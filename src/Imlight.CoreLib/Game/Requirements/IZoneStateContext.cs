using System;
using Imlight.CoreLib.Game.Zone.Core;

namespace Imlight.CoreLib.Game.Requirements;

/// <summary>
/// A requirement context that can read the state of the zone instance the check is about: object states, and
/// the tokens, counters, puzzle variables and trigger states of its trigger scripts.
/// </summary>
public interface IZoneStateContext {

    /// <summary>
    /// The tokens, counters, puzzle variables and trigger states of the zone (null outside a zone).
    /// </summary>
    ZoneScriptState ScriptState { get; }

    /// <summary>
    /// Judges the completion of dungeon quests by the instance a player's entry grants them in, instead of by
    /// the player's registry: true or false for a quest the instance judges, null for any other (and by default).
    /// </summary>
    Func<string, bool?> InstanceQuestCompleted => null;

}
