using Imlight.CoreLib.Game.Zone.Core;

namespace Imlight.CoreLib.Game.Requirements;

/// <summary>
/// A requirement context that can read the state of the zone instance the check is about: object states, and
/// the tokens, counters, puzzle variables and trigger states of its trigger scripts.
/// </summary>
public interface IZoneStateContext {

    /// <summary>The state of every named object in the zone (null outside a zone).</summary>
    ZoneObjectStates ObjectStates { get; }

    /// <summary>The tokens, counters, puzzle variables and trigger states of the zone (null outside a zone).</summary>
    ZoneScriptState ScriptState { get; }

}
