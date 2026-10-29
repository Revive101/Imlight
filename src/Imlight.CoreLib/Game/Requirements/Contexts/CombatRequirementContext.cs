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
 *
 * ========================================================================
 * COMBAT REQUIREMENT CONTEXT
 * ========================================================================
 * 
 * PURPOSE:
 * Gives the requirement handlers of a conditional spell effect the two participants
 * they test: the caster and the current target of the effect.
 * 
 * USAGE EXAMPLE:
 * var context = new CombatRequirementContext(requirements, caster, target, duel);
 * var met = RequirementDispatcher.EvaluateRequirements(requirements, context);
 * 
 * NOTE:
 * A requirement names its participant by m_targetType; a missing target falls back to the caster.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Requirements.Contexts;

/// <summary>
/// The requirement context of a conditional spell effect element.
/// </summary>
/// <param name="requirements">The requirements being evaluated.</param>
/// <param name="caster">The participant that cast the spell.</param>
/// <param name="target">The participant the conditional effect is currently applied to.</param>
/// <param name="duel">The duel the spell is cast in.</param>
internal sealed class CombatRequirementContext(RequirementList requirements,
                                               CombatDuelSubCircle caster,
                                               CombatDuelSubCircle target,
                                               CombatDuelComponent duel) : IRequirementContext {

    /// <summary>
    /// The participant that cast the spell.
    /// </summary>
    internal CombatDuelSubCircle Caster { get; } = caster;

    /// <summary>
    /// The participant the conditional effect is currently applied to.
    /// </summary>
    internal CombatDuelSubCircle Target { get; } = target;

    /// <summary>
    /// The duel the spell is cast in.
    /// </summary>
    internal CombatDuelComponent Duel { get; } = duel;

    /// <summary>
    /// Gets the participant a requirement tests.
    /// </summary>
    /// <param name="requirement">The requirement to resolve.</param>
    /// <returns>The target for RT_Target when there is one, otherwise the caster.</returns>
    internal CombatDuelSubCircle GetParticipant(ConditionalSpellEffectRequirement requirement)
        => requirement.m_targetType == RequirementTarget.RT_Target && Target is not null
            ? Target
            : Caster;

    public RequirementList GetFullRequirementList() => requirements;
    public List<Requirement> GetRequirements() => requirements?.m_requirements;
    public IActorRef GetPlayerRef() => Caster?.ParticipantActor;
    public CoreObject GetPlayerObj() => Caster?.ParticipantObject;
    public Wizard GetWizard() => Caster?._wizard;
    public IActorRef GetZoneRef() => null;
    public string GetQuestName() => null;
    public string GetGoalName() => null;
    public string GetTriggerName() => null;

}
