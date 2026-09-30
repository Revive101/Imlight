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
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 * ========================================================================
 * INTERACT OBJECT STATE COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Makes a zone object that changes state when used (levers, braziers, obelisks, candles, crystal
 * stands) a press-X interactable. Using it puts the object into the state its option names, raises
 * "<object>.<state>.EnterState" and runs the option's results (which post events such as LeverUsed).
 *
 * USAGE EXAMPLE:
 * Nothing to configure: any object template whose interact option names a state, or whose option
 * results post an event, gets this component.
 *
 * NOTE:
 * An option that carries goal tags belongs to a quest usage goal: InteractQuestSelectComponent offers it
 * and calls <see cref="ApplyGoalOptions"/> when the goal is used (which also applies the plain state options, so
 * goal-less stands still raise their EnterState). Every other option is a candidate while its requirements
 * hold and the object is in the option's visible state. The client gets one press-X entry per object; each
 * use applies the option that fits the current state (see ChooseOption), so two- and four-state objects step
 * through their states. Options whose results show dialog make a readable object (a book) attach too.
 *
 * TODO:
 *
 * Version: KALI 1.0
 * Last Updated: 09/30/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractObjectStateComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "Interact";
    public string NpcIcon => (Entity.Template as GameObjectTemplate)?.m_sIcon;
    public string NpcNameKey => (Entity.Template as GameObjectTemplate)?.m_displayName;
    public string NpcTextKey => null;
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private string ObjectName => Entity.Info?.m_zoneTag is { Length: > 0 } tag
        ? tag
        : (Entity.Template as GameObjectTemplate)?.m_objectName.ToString();

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate go && GetStateOptions(go).Any(ChangesWorld);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter)
        => GetOfferedOptions(playerCharacter).Any()
            ? [new InteractableOption { m_serviceName = ServiceName }]
            : [];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        // One press-X entry stands for the whole object; the memento hands each component the index of its own option.
        if (serviceOptionIndex != 0) {
            return;
        }

        var chosen = ChooseOption(GetOfferedOptions(playerCharacter).ToList(), CurrentState());
        if (chosen is not null) {
            Apply(chosen, playerActor, playerObject);
        }
    }

    /// <summary>
    /// The option one use applies. Options with requirements are the specific ones (the offered list already
    /// holds only those that pass), so they win over unconditional ones. Among several, the use steps to the option
    /// after the one matching the current state, wrapping around; an unmatched state starts at the first.
    /// </summary>
    internal static InteractStateOptionTemplate ChooseOption(IReadOnlyList<InteractStateOptionTemplate> offered, string currentState) {
        if (offered.Count == 0) {
            return null;
        }

        var pool = offered.Where(o => o.m_requirements?.m_requirements is { Count: > 0 }).ToList();
        if (pool.Count == 0) {
            pool = [.. offered];
        }

        if (pool.Count == 1) {
            return pool[0];
        }

        var at = string.IsNullOrEmpty(currentState)
            ? -1
            : pool.FindIndex(o => string.Equals(o.m_enterState.ToString(), currentState, StringComparison.OrdinalIgnoreCase));

        return pool[(at + 1) % pool.Count];
    }

    // The state the object is in now, else the state its placement starts it in.
    private string CurrentState()
        => Zone.ObjectStates.Get(ObjectName) is { Length: > 0 } known ? known : Entity.Info?.m_startState;

    /// <summary>
    /// Applies the object's options for a quest goal use: every goal-tagged option, plus the plain state
    /// options whose visible state matches (crystal stands carry no goal tags, quests name them by object).
    /// A plain option is skipped once the object is already in its state, so it never applies twice.
    /// </summary>
    public void ApplyGoalOptions(IActorRef playerActor, CoreObject playerObject) {
        var current = Zone.ObjectStates.Get(ObjectName);
        foreach (var option in GetStateOptions(Entity.Template as GameObjectTemplate)) {
            if (IsGoalOption(option)) {
                Apply(option, playerActor, playerObject);
                continue;
            }

            if (!ChangesWorld(option)) {
                continue;
            }

            var visibleState = option.m_visibleState.ToString();
            if (!string.IsNullOrEmpty(visibleState) && current is not null
                && !string.Equals(current, visibleState, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var enterState = option.m_enterState.ToString();
            if (!string.IsNullOrEmpty(enterState) && string.Equals(current, enterState, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            Apply(option, playerActor, playerObject);
        }
    }

    private void Apply(InteractStateOptionTemplate option, IActorRef playerActor, CoreObject playerObject) {
        var objectName = ObjectName;
        var newState = option.m_enterState.ToString();
        if (!string.IsNullOrEmpty(newState) && !string.IsNullOrEmpty(objectName)) {
            Zone.ObjectStates.Set(objectName, newState);
            Entity.ChangeState(newState);

            ZoneActor.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = $"{objectName}.{newState}.EnterState",
                PlayerActor = playerActor,
                PlayerGameObject = playerObject,
            });
        }

        if (option.m_results?.m_results is { Count: > 0 }) {
            Entity.ExecuteResults(option.m_results, playerActor, playerObject, objectName);
        }
    }

    private IEnumerable<InteractStateOptionTemplate> GetOfferedOptions(Wizard wizard) {
        var current = CurrentState();

        foreach (var option in GetStateOptions(Entity.Template as GameObjectTemplate)) {
            if (IsGoalOption(option) || !ChangesWorld(option)) {
                continue;
            }

            // An unknown current state offers everything; a known one must match the option's.
            var visibleState = option.m_visibleState.ToString();
            if (!string.IsNullOrEmpty(visibleState) && current is not null
                && !string.Equals(current, visibleState, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var requirements = option.m_requirements;
            if (requirements?.m_requirements is { Count: > 0 }
                && !RequirementDispatcher.EvaluateRequirements(
                    requirements,
                    new ZoneRequirementContext(requirements, null, null, wizard, ZoneActor, ObjectName) {
                        ObjectStates = Zone.ObjectStates,
                        ScriptState = Zone.ScriptState,
                    })) {
                continue;
            }

            yield return option;
        }
    }

    private static bool IsGoalOption(InteractStateOptionTemplate option)
        => option.m_goalTags is { Count: > 0 };

    private static bool ChangesWorld(InteractStateOptionTemplate option)
        => !string.IsNullOrEmpty(option.m_enterState.ToString())
        || option.m_results?.m_results?.Any(r => r is ResPostEvent or ResModifyTriggerObject or ResActorDialog) == true;

    private static IEnumerable<InteractStateOptionTemplate> GetStateOptions(GameObjectTemplate template)
        => template?.m_behaviors?
            .OfType<InteractableBehaviorTemplate>()
            .SelectMany(behavior => behavior.m_interactOptions ?? [])
            .OfType<InteractStateOptionTemplate>()
        ?? [];

}
