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
 * RENDER COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages object rendering and visibility mechanics for game entities, 
 * handling player-specific object creation, spawning, and despawning.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Supports dynamic object rendering based on player proximity.
 * There are two distinct messages to send to the client in regards to objects in the game.
 * The first is MSG_NEWOBJECT, which is used to create a new object in the client's world.
 * The second is MSG_ADDOBJECT, which respawns an object that was previously removed.
 * You cannot send MSG_ADDOBJECT in regards to an object if the client has not been told about it with MSG_NEWOBJECT.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 07/02/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class RenderComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    private const string SPAWN_STATE_NAME = "On";
    private const string DESPAWN_STATE_NAME = "Off";

    private readonly CoreObjectSerializer _serializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );
    private readonly PropertyFlags _propertyFlags = PropertyFlags.Prop_Public
                                                  | PropertyFlags.Prop_Transmit
                                                  | PropertyFlags.Prop_AuthorityTransmit;
    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private readonly Dictionary<Wizard, IActorRef> _playersWithRequirementsMet = [];
    private readonly List<IActorRef> _playerIgnoreBecauseDynamod = [];
    private float _renderDistance;
    private bool _doesDistanceCheck = false;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is RenderBehaviorTemplate)
        || template is CombatSigilTemplate; // bug fix: combat sigil templtaes don't have any behaviors

    public override void OnStart() {
        // A combat minion never distance-culls; it lives only for the duel.
        _doesDistanceCheck = !Entity.IsCombatOnlyMinion
            && Entity.Template.m_behaviors
                .OfType<AnimationBehaviorTemplate>()
                .Any(anim => anim.m_bFadesIn || anim.m_bFadesOut);

        _renderDistance = Entity.Zone.ZoneData.m_farClip;

        CreateObjectForAllPlayers();
    }

    public override void OnEnabled()
        => CreateObjectForAllPlayers();

    public override void OnDisabled() =>
        // Broadcast the removal of the object to all players.
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        });

    public override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        // Check to see if dynamods would enable/disable this object.
        // We don't need to check for spawns, only despawns.
        var relevantDynaMods = wizard?.DynamodSet?.Dynamods?
            .Where(d => d.ClientTag.Equals(Entity.Info?.m_zoneTag, System.StringComparison.OrdinalIgnoreCase))
            .Where(d => string.IsNullOrEmpty(d.ZoneName) 
                     || d.ZoneName.Equals(Entity.Zone.ZoneData.m_zoneName, System.StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];
        foreach (var mod in relevantDynaMods) {
            // If the player has a dynamod that disables this object, do not spawn it for them.
            if (mod.ModState.Equals(DESPAWN_STATE_NAME, System.StringComparison.OrdinalIgnoreCase)) {
                _playerIgnoreBecauseDynamod.Add(suspect);

                return;
            }
        }

        // Determine if this player meets the requirements to see the object.
        var requirementsMet = true;
        if (Entity.Info is not null && Entity.Info.m_spawnRequirements is not null) {
            var requirements = Entity.Info.m_spawnRequirements;
            requirementsMet = RequirementDispatcher.EvaluateRequirements(
                requirements,
                new ZoneRequirementContext(requirements, suspect, null, wizard, Entity.ZoneRef)
            );
        }

        if (requirementsMet) {
            _playersWithRequirementsMet.Add(wizard, suspect);

            // Always send MSG_NEWOBJECT so the client registers this object,
            // even if the player is outside the render distance. Without this,
            // the client will ignore subsequent MSG_ADDOBJECT messages.
            // See: "You cannot send MSG_ADDOBJECT in regards to an object if
            // the client has not been told about it with MSG_NEWOBJECT."
            CreateObjectForPlayer(suspect);

            // Always add the player to the in-range list so the next
            // OnPlayerMove tick can correctly evaluate distance and send
            // MSG_REMOVEOBJECT if the player is outside the render radius.
            // The client needs the ~250ms gap between MSG_NEWOBJECT and
            // any MSG_REMOVEOBJECT to register the object properly.
            _playersInRange.Add(player, suspect);

            return;
        }

        // If the player doesn't meet spawn requirements, nothing to do.
        if (!_doesDistanceCheck) {
            return;
        }

        if (!IsInRadius(player, _renderDistance)) {
            DespawnObjectForPlayer(suspect);
        }
        else {
            _playersInRange.Remove(player);
            _playersInRange.Add(player, suspect);
        }
    }

    public override void OnPlayerLeave(IActorRef suspect, ulong id) {
        var wizard = _playersWithRequirementsMet.FirstOrDefault(x => x.Value == suspect).Key;
        if (wizard != null) {
            _playersWithRequirementsMet.Remove(wizard);
        }

        // Remove the player from the list of players in range.
        var player = _playersInRange.FirstOrDefault(x => x.Value == suspect).Key;
        if (player != null) {
            _playersInRange.Remove(player);
        }

        if (_playerIgnoreBecauseDynamod.Contains(suspect)) {
            _playerIgnoreBecauseDynamod.Remove(suspect);

            return;
        }

        DespawnObjectForPlayer(suspect);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!_doesDistanceCheck) {
            return;
        }

        // If this player is ignoring the object due to a dynamod, do nothing.
        if (_playerIgnoreBecauseDynamod.Contains(playerActor)) {
            return;
        }

        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, _renderDistance) && !_playersInRange.ContainsKey(playerObj)) {
            // Respawn the object if the player is in range and we've determined they meet the requirements.
            if (playerWizard is not null && _playersWithRequirementsMet.ContainsKey(playerWizard)) {
                RespawnObjectForPlayer(playerActor);
            }

            _playersInRange.Add(playerObj, playerActor);
        }
        else if (!IsInRadius(playerObj, _renderDistance) && _playersInRange.ContainsKey(playerObj)) {
            // If the player is out of range, despawn the object for them.
            DespawnObjectForPlayer(playerActor);
            _playersInRange.Remove(playerObj);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTERSTATE))]
    public void ReceiveEnterState(ZONE_102_PROTOCOL.MSG_ENTERSTATE msg) {
        // In the context of the RenderComponent, we only care if "On" or "Off" is the state.
        var isDespawn = msg.StateName.Equals(DESPAWN_STATE_NAME, System.StringComparison.OrdinalIgnoreCase);
        var isSpawn = msg.StateName.Equals(SPAWN_STATE_NAME, System.StringComparison.OrdinalIgnoreCase);
        if (!isDespawn && !isSpawn) {
            return;
        }

        // If so, despawn the object for the sender if the tag matches.
        var zoneTag = msg.ObjectName;
        if (Entity.Info is not null && Entity.Info.m_zoneTag.Equals(zoneTag, System.StringComparison.OrdinalIgnoreCase)) {
            var player = msg.Sender;
            if (player is null) {
                return;
            }

            if (isDespawn) {
                DespawnObjectForPlayer(player);
                _playerIgnoreBecauseDynamod.Add(player);
            }
            else if (isSpawn) {
                // Only respawn the object if the player meets the requirements.
                var wizard = _playersWithRequirementsMet.FirstOrDefault(x => x.Value == player).Key;
                if (wizard is not null) {
                    CreateObjectForPlayer(player);
                    _playerIgnoreBecauseDynamod.Remove(player);
                }
            }
        }
    }

    private void CreateObjectForPlayer(IActorRef player) {
        // Serialize the client object.
        var clientObj = Entity.GetClientBehaviorInstance();
        if (!_serializer.Serialize(clientObj, _propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize object data.");

            return;
        }

        // Send object data to the player.
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = serializedData
        };
        player.Tell(newObjectMsg);
    }

    private void CreateObjectForAllPlayers() {
        // Serialize the client object.
        var clientObj = Entity.GetClientBehaviorInstance();
        if (!_serializer.Serialize(clientObj, _propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize object data.");

            return;
        }

        // Send object data to all players.
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = serializedData
        });
    }

    private void RespawnObjectForPlayer(IActorRef player) {
        // Send object data to the player.
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_ADDOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID,
            LocationX = Entity.ActiveGameObject.m_location.X,
            LocationY = Entity.ActiveGameObject.m_location.Y,
            LocationZ = Entity.ActiveGameObject.m_location.Z,
            Direction = Entity.ActiveGameObject.m_orientation.Z
        };
        player.Tell(newObjectMsg);
    }

    private void DespawnObjectForPlayer(IActorRef player) {
        // Send object data to the player
        var despawnObjectMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };
        player.Tell(despawnObjectMsg);
    }

}