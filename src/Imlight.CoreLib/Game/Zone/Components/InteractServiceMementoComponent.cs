/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * INTERACT SERVICE MEMENTO
 * ========================================================================
 * 
 * PURPOSE:
 * Manages complex NPC interaction mechanics, tracking player proximity 
 * and service component interactions for zone entities.
 * 
 * USAGE EXAMPLE:
 * Always attached to an entity. Implement a service component to add functionality.
 * 
 * NOTE:
 * Keeps track of any components of type `IServiceComponent` attached to the entity.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Interface for service components that can be attached to a zone entity.
/// </summary>
public interface IServiceComponent {

    IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter);
    void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex);

    string ServiceName { get; }
    string NpcIcon { get; }
    string NpcNameKey { get; }
    string NpcTextKey { get; }
    WizBangs WizBang { get; }
    string StateName { get; }
    string InteractWizBang { get; }
    string DisplayKey { get; }

}

internal sealed class InteractServiceMementoComponent(ZoneEntity entity) 
    : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const string DEFAULT_NAME_KEY = "NPCFormats_Name";
    private const string DEFAULT_TEXT_KEY = "GUI_NPCInteractText";
    private const string WIZBANG_UPDATE_TIMER_KEY = "WIZBANG_UPDATE_TIMER";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 1.0;
    private const float DEFAULT_RENDER_DISTANCE = 5000.0f; // Default wizbang render distance.

    private readonly float _interactionRadius = 300.0f;
    private readonly Dictionary<ulong, IActorRef> _playersInInteractionRange = [];
    private readonly Dictionary<ulong, IActorRef> _playersInRenderRange = [];
    private readonly Dictionary<ulong, WizBangs> _lastSentWizBangs = [];
    private List<IServiceComponent> _serviceComponents = [];
    private Dictionary<int, IServiceComponent> _optionIndexToComponent = [];
    private ServiceMementoBase _serviceMemento;
    private MadlibBlock _madlibBlock;
    private float _renderDistance;

    public ITimerScheduler Timers { get; set; }

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => true;

    public override void OnStart() {
        // Set render distance - use zone far clip if available, otherwise default.
        _renderDistance = Entity.Zone?.ZoneData?.m_farClip ?? DEFAULT_RENDER_DISTANCE;
        
        RefreshServiceMomento(null);

        var updateInterval = TimeSpan.FromSeconds(WIZBANG_UPDATE_INTERVAL_SECONDS);
        var updateMsg = new ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL();
        Timers.StartPeriodicTimer(WIZBANG_UPDATE_TIMER_KEY, updateMsg, updateInterval);
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        if (_playersInInteractionRange.Any(x => x.Value == playerActor)) {
            var playerObj = _playersInInteractionRange.First(x => x.Value == playerActor).Key;
            _playersInInteractionRange.Remove(playerObj);
        }
        
        if (_playersInRenderRange.Any(x => x.Value == playerActor)) {
            var playerObj = _playersInRenderRange.First(x => x.Value == playerActor).Key;
            _playersInRenderRange.Remove(playerObj);
        }

        _lastSentWizBangs.Remove(id);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        var playerId = playerObj.m_globalID.Full;

        // Handle interaction range (for service options).
        if (IsInRadius(playerObj, _interactionRadius)
            && !_playersInInteractionRange.ContainsKey(playerId)) {
            _playersInInteractionRange.Add(playerId, playerActor);
            SendActorServiceOptions(playerActor);
        }
        else if (!IsInRadius(playerObj, _interactionRadius)
                 && _playersInInteractionRange.ContainsKey(playerId)) {
            _playersInInteractionRange.Remove(playerId);
            SendLeaveServiceRange(playerActor);
        }

        // Handle render range (for wizbangs).
        if (IsInRadius(playerObj, _renderDistance)) {
            if (!_playersInRenderRange.ContainsKey(playerId)) {
                _playersInRenderRange.Add(playerId, playerActor);
            }
        }
        else if (_playersInRenderRange.ContainsKey(playerId)) {
            _playersInRenderRange.Remove(playerId);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEINTERACTION))]
    private void PlayerInteraction(ZONE_102_PROTOCOL.MSG_ZONEINTERACTION message) {
        var playerActor = message.PlayerActor;
        var playerCharacter = message.PlayerCharacter;
        var playerObject = message.PlayerObject;
        var serviceName = message.ServiceName;
        var serviceIndex = message.ServiceIndex;
        var reinteract = message.Reinteract;

        Logger.Debug("Player {0} interacted with NPC {1} using service {2} at index {3} (Reinteract: {4})",
            Logger.Args(playerActor.Path.Name, Entity.ActiveGameObject.m_globalID.Full, serviceName, serviceIndex, reinteract));

        if (_serviceComponents.Count <= 0) {
            Logger.Warning("No service components found for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        // If this is a reinteract scenario, send service range messages first.
        if (reinteract == 2) {
            SendLeaveServiceRange(playerActor);
            SendActorServiceOptions(playerActor, reinteract);
        }

        // Route to the component that owns this option index.
        // Using FirstOrDefault by ServiceName is unsafe when multiple components share
        // the same service name (e.g. InteractQuestSelectComponent shadowing WoodenChestComponent).
        if (!_optionIndexToComponent.TryGetValue((int) serviceIndex, out var serviceComponent)) {
            Logger.Warning("No component owns service index {0} for NPC {1} with service name {2}",
                Logger.Args(serviceIndex, Entity.ActiveGameObject.m_debugName, serviceName));

            return;
        }

        // Call the service component's interaction method.
        serviceComponent.OnServiceInteraction(playerActor, playerCharacter, playerObject, serviceIndex);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL))]
    private void HandleWizBangUpdateInterval(ZONE_102_PROTOCOL.MSG_WIZBANGUPDATEINTERVAL message) {
        if (_serviceComponents.Count <= 0 || _playersInRenderRange.Count == 0) {
            return;
        }

        // Query every nearby player's wizard concurrently instead of
        // blocking on each one sequentially.
        var playerActors = _playersInRenderRange.Values.ToArray();
        var queryTasks = playerActors.Select(async playerActor => {
            try {
                var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
                var rsp = await playerActor.Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);
                return rsp.Wizard;
            }
            catch {
                return null;
            }
        });

        Task.WhenAll(queryTasks)
            .ContinueWith(t => {
                var wizards = t.Result.ToArray();
                return new ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT {
                    PlayerActors = playerActors,
                    Wizards = wizards
                };
            })
            .PipeTo(Self);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT))]
    private void HandleWizBangUpdateResult(ZONE_102_PROTOCOL.MSG_WIZBANG_UPDATE_RESULT result) {
        for (int i = 0; i < result.PlayerActors.Length; i++) {
            var wizard = result.Wizards[i];
            if (wizard != null) {
                SendWizBang(result.PlayerActors[i], wizard);
            }
        }
    }

    private void SendActorServiceOptions(IActorRef playerActor, int reinteract = 0) {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = playerActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        RefreshServiceMomento(wizard);

        // If we have no service options, do not send anything.
        if (_serviceMemento.m_serviceOptions.Count <= 0) {
            return;
        }

        // Serialize the service memento and send it to the player.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(_serviceMemento, 4, out var data)) {
            Logger.Error("Failed to serialize service memento for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID.Full,
            Options = data,
            Reinteract = reinteract
        };

        playerActor.Tell(npcOptionsMsg);
    }

    private void SendLeaveServiceRange(IActorRef playerActor) {
        var msg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            // Do not let this property name fool you. 
            // The client incorrectly labels this property as "MobileID." It is in fact
            // the global ID of the NPC. It will not work if you set it to the mobile ID.
            MobileID = Entity.ActiveGameObject.m_globalID.Full
        };

        playerActor.Tell(msg);
    }

    private void RefreshServiceMomento(Wizard playerCharacter) {
        _serviceComponents = [.. Entity.GetComponentsOfType<IServiceComponent>()];
        if (_serviceComponents.Count <= 0) {
            return;
        }

        var gameObjTemplate = Entity.Template as GameObjectTemplate;

        // Get all service options and track which component owns each flat index.
        _optionIndexToComponent.Clear();
        var allOptions = new List<ServiceOptionBase>();
        foreach (var component in _serviceComponents) {
            var componentOptions = component.GetServiceOptions(playerCharacter).ToList();
            foreach (var option in componentOptions) {
                _optionIndexToComponent[allOptions.Count] = component;
                allOptions.Add(option);
            }
        }

        // Get UI overrides based on priority.
        var sortedComponents = SortComponentsByPriority(_serviceComponents);
        var highestPriority = sortedComponents.FirstOrDefault();

        SetMadLibBlock();

        var npcIconOrDefault = string.IsNullOrEmpty(highestPriority?.NpcIcon)
            ? gameObjTemplate?.m_sIcon
            : highestPriority.NpcIcon;
        var npcNameKeyOrDefault = string.IsNullOrEmpty(highestPriority?.NpcNameKey)
            ? DEFAULT_NAME_KEY
            : highestPriority.NpcNameKey;
        var npcTextKeyOrDefault = string.IsNullOrEmpty(highestPriority?.NpcTextKey)
            ? DEFAULT_TEXT_KEY
            : highestPriority.NpcTextKey;

        _serviceMemento = new ServiceMementoBase {
            m_npcIcon = npcIconOrDefault ?? string.Empty,
            m_npcNameKey = npcNameKeyOrDefault ?? DEFAULT_NAME_KEY,
            m_npcTextKey = npcTextKeyOrDefault ?? DEFAULT_TEXT_KEY,
            m_serviceOptions = allOptions,
            m_personaMadlibs = _madlibBlock
        };
    }

    private void SetMadLibBlock() {
        // NPCs normally have a madlib of first name, last name, and title.
        // To avoid hardcoding these values, we use the display name of the template.
        // We'll also set the madlib token to just "NAME" so the client displays the name as-is.
        if (Entity.Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var madlibList = new List<MadlibArg> {
            new MadlibArgT_ByteString() {
                m_madlibArgument = gameObjTemplate.m_displayName,
                m_madlibToken = "NAME"
            },
        };

        _madlibBlock = new MadlibBlock() {
            m_blockToken = "NPC",
            m_madlibs = madlibList
        };
    }

    private void SendWizBang(IActorRef playerActor, Wizard playerWizard) {
        if (_serviceComponents.Count <= 0) {
            return;
        }

        // Collect wizbangs from components that have service options for this player.
        var activeWizBangs = new List<WizBangs>();
        foreach (var component in _serviceComponents) {
            var serviceOptions = component.GetServiceOptions(playerWizard);
            if (serviceOptions.Any()) {
                activeWizBangs.Add(component.WizBang);
            }
        }

        // If there are no wizbangs, don't continue.
        if (activeWizBangs.Count <= 0) {
            return;
        }

        // Get highest priority wizbang for this player.
        var wizBang = WizBangs.None;
        if (activeWizBangs.Count > 0) {
            var priorityWizBang = WizBangPriority.GetHighestPriorityWizBang(activeWizBangs);
            wizBang = priorityWizBang;
        }

        var playerId = playerWizard.CharId;

        // Check if this wizbang is different from the last one we sent.
        if (_lastSentWizBangs.TryGetValue(playerId, out var lastWizBang) && lastWizBang == wizBang) {
            return; 
        }

        // Cache the new wizbang and send the message.
        _lastSentWizBangs[playerId] = wizBang;

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) wizBang,
            GameObjectID = Entity.ActiveGameObject.m_globalID.Full
        };

        playerActor.Tell(wizBangMsg);
    }

    private static IOrderedEnumerable<IServiceComponent> SortComponentsByPriority(IEnumerable<IServiceComponent> components) {
        // Sort by WizBang priority
        var wizBangs = components.Select(c => c.WizBang);
        var prioritySortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs([.. wizBangs]);

        // If priority sorted WizBangs are empty, sort by default.
        if (prioritySortedWizBangs is null || prioritySortedWizBangs.Count <= 0) {
            return components.OrderBy(c => c.WizBang);
        }

        return components.OrderBy(c => prioritySortedWizBangs.IndexOf(c.WizBang));
    }

}