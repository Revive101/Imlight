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
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements;

/// <summary>
/// Provides context information for requirement evaluation
/// </summary>
public interface IRequirementContext {

    /// <summary>
    /// Gets the full requirement list, including operator type
    /// </summary>
    /// <returns>The full requirement list</returns>
    RequirementList GetFullRequirementList();

    /// <summary>
    /// Gets the requirements to evaluate
    /// </summary>
    /// <returns>The list of requirements</returns>
    List<Requirement> GetRequirements();

    /// <summary>
    /// Gets the player actor reference
    /// </summary>
    /// <returns>The player actor reference</returns>
    IActorRef GetPlayerRef();

    /// <summary>
    /// Gets the player game object
    /// </summary>
    /// <returns>The player game object</returns>
    CoreObject GetPlayerObj();

    /// <summary>
    /// Gets the wizard (player data)
    /// </summary>
    /// <returns>The wizard object</returns>
    Wizard GetWizard();

    /// <summary>
    /// Gets the zone actor reference if applicable
    /// </summary>
    /// <returns>The zone actor reference, or null if not applicable</returns>
    IActorRef GetZoneRef();

    /// <summary>
    /// Gets the quest name if this is a quest-related requirement check
    /// </summary>
    /// <returns>The quest name, or null if not quest-related</returns>
    string GetQuestName();

    /// <summary>
    /// Gets the goal name if this is a goal-related requirement check
    /// </summary>
    /// <returns>The goal name, or null if not goal-related</returns>
    string GetGoalName();

    /// <summary>
    /// Gets the trigger name if this is a trigger-related requirement check
    /// </summary>
    /// <returns>The trigger name, or null if not trigger-related</returns>
    string GetTriggerName();

}