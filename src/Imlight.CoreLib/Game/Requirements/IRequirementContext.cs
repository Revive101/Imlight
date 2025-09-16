/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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