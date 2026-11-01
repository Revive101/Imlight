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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System.Collections.Generic;

namespace Imlight.PluginApi.Events;

/// <summary>
/// Fired when a duel has been created with its starting participants, before the first round.
/// <see cref="IZone.Duels"/> already includes it. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelStart(IDuel duel) : ImlightEvent {

    /// <summary>
    /// The duel that started. <see cref="IDuel.Round"/> is 0.
    /// </summary>
    public IDuel Duel { get; } = duel;

}

/// <summary>
/// Fired when a new round of a duel begins, before its planning phase. Creatures may already be choosing their moves.
/// Fires on the duel's actor.
/// </summary>
public sealed class OnDuelRoundStart(IDuel duel) : ImlightEvent {

    /// <summary>
    /// The duel. <see cref="IDuel.Round"/> is already the new round.
    /// </summary>
    public IDuel Duel { get; } = duel;

}

/// <summary>
/// Fired once when a duel is decided, before rewards are given and before <see cref="OnPlayerDeath"/> fires. Fires on
/// the duel's actor.
/// </summary>
public sealed class OnDuelEnd(IDuel duel, IReadOnlyList<IPlayer> winners) : ImlightEvent {

    /// <summary>
    /// The duel that ended. <see cref="IDuel.Players"/> and <see cref="IDuel.Creatures"/> still hold who was left in it.
    /// </summary>
    public IDuel Duel { get; } = duel;

    /// <summary>
    /// The players on the winning side, including any knocked out. Empty if the creatures won or a PvP match drew.
    /// </summary>
    public IReadOnlyList<IPlayer> Winners { get; } = winners;

}

/// <summary>
/// Fired when a player or creature joins a duel already running, including a minion being summoned into it. Starting
/// participants don't fire it; read them from <see cref="OnDuelStart"/>. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistJoin(IDuelist duelist, IDuel duel) : ImlightEvent {

    /// <summary>
    /// The player or creature that joined.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The duel they joined. <see cref="IDuel.Players"/> or <see cref="IDuel.Creatures"/> already includes them.
    /// </summary>
    public IDuel Duel { get; } = duel;

}

/// <summary>
/// Fired when a player leaves a duel before it ends, by fleeing, forfeiting, disconnecting or logging out. Players
/// still in the duel when it ends don't fire it. Fires on the duel's actor.
/// </summary>
public sealed class OnPlayerDuelLeave(IPlayer player, IDuel duel) : ImlightEvent {

    /// <summary>
    /// The player who left.
    /// </summary>
    public IPlayer Player { get; } = player;

    /// <summary>
    /// The duel they left. <see cref="IDuel.Players"/> no longer includes them.
    /// </summary>
    public IDuel Duel { get; } = duel;

}

/// <summary>
/// Fired when a player or creature loses health in a duel, as the server resolves the round and before the client
/// plays it. Hits fully absorbed by wards or resistances don't fire it. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistDamage(IDuelist duelist, int damage) : ImlightEvent {

    /// <summary>
    /// The player or creature that was damaged. <see cref="IDuelist.Health"/> is already reduced.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The health they lost, never more than they had.
    /// </summary>
    public int Damage { get; } = damage;

}

/// <summary>
/// Fired when a player's, creature's or minion's queued spell is about to be cast in a duel, before it is rolled for a
/// fizzle. Passes, and turns lost to a stun or a dead target, don't fire it. Cancelling it turns the cast into a pass,
/// so a player spends no pips, mana or card. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistSpellCast(IDuelist duelist, uint spellTemplateId, string spellName, string school,
    int pipCost) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player or creature that is casting.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The template ID of the spell being cast.
    /// </summary>
    public uint SpellTemplateId { get; } = spellTemplateId;

    /// <summary>
    /// The internal name of the spell's template.
    /// </summary>
    public string SpellName { get; } = spellName;

    /// <summary>
    /// The spell's school, e.g. <c>Fire</c>.
    /// </summary>
    public string School { get; } = school;

    /// <summary>
    /// The spell's rank in pips.
    /// </summary>
    public int PipCost { get; } = pipCost;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player or creature gains health in a duel, from a spell or a heal over time, as the server resolves the
/// round and before the client plays it. Heals on someone already at full health don't fire it. Fires on the duel's
/// actor.
/// </summary>
public sealed class OnDuelistHeal(IDuelist duelist, int healing) : ImlightEvent {

    /// <summary>
    /// The player or creature that was healed. <see cref="IDuelist.Health"/> is already raised.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The health they gained, never more than they were missing.
    /// </summary>
    public int Healing { get; } = healing;

}

/// <summary>
/// Fired when a spell that passed <see cref="OnDuelistSpellCast"/> fails to go off, by missing its accuracy roll or by
/// being stopped by a dispel on the caster. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistSpellFizzle(IDuelist duelist, uint spellTemplateId, string spellName, string school,
    bool wasDispelled) : ImlightEvent {

    /// <summary>
    /// The player or creature whose spell fizzled.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The template ID of the spell.
    /// </summary>
    public uint SpellTemplateId { get; } = spellTemplateId;

    /// <summary>
    /// The internal name of the spell's template.
    /// </summary>
    public string SpellName { get; } = spellName;

    /// <summary>
    /// The spell's school, e.g. <c>Fire</c>.
    /// </summary>
    public string School { get; } = school;

    /// <summary>
    /// True if a dispel on the caster stopped the spell, false if it missed its accuracy roll.
    /// </summary>
    public bool WasDispelled { get; } = wasDispelled;

}

/// <summary>
/// Fired when a player's, creature's or minion's turn in a duel goes by without a spell. A caster that an over-time
/// effect kills at the start of its turn doesn't fire it, and neither does a spell that resolves with nothing worth
/// casting, though the client plays that as a pass. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistPass(IDuelist duelist, DuelPassReason reason) : ImlightEvent {

    /// <summary>
    /// The player or creature that passed.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// Why they passed.
    /// </summary>
    public DuelPassReason Reason { get; } = reason;

}

/// <summary>
/// Fired when a player, creature or minion is about to be stunned in a duel, after any stun block and stun resistance
/// have failed to stop it. Bosses, who can't be stunned, don't fire it. Cancelling it treats the target as immune, so
/// they get no stun block either. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistStun(IDuelist duelist, int rounds) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player or creature being stunned.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// How many of their turns the stun will take.
    /// </summary>
    public int Rounds { get; } = rounds;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a player, creature or minion is about to be mind controlled in a duel. Bosses, who can't be mind
/// controlled, don't fire it, and neither does a target already on the caster's team. Cancelling it leaves them on their
/// own team. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistMindControl(IDuelist duelist, int turns) : ImlightEvent, ICancellable {

    /// <summary>
    /// The player or creature being mind controlled.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// How many of their turns the mind control will last.
    /// </summary>
    public int Turns { get; } = turns;

    /// <inheritdoc />
    public bool IsCancelled { get; set; }

}

/// <summary>
/// Fired when a spell lands a critical, once per cast however many targets it hit, after its effects have been applied
/// and its cost paid. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistSpellCritical(IDuelist duelist, uint spellTemplateId, string spellName, string school,
    float multiplier) : ImlightEvent {

    /// <summary>
    /// The player or creature whose spell landed a critical.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

    /// <summary>
    /// The template ID of the spell.
    /// </summary>
    public uint SpellTemplateId { get; } = spellTemplateId;

    /// <summary>
    /// The internal name of the spell's template.
    /// </summary>
    public string SpellName { get; } = spellName;

    /// <summary>
    /// The spell's school, e.g. <c>Fire</c>.
    /// </summary>
    public string School { get; } = school;

    /// <summary>
    /// The multiplier the critical applied to the spell's damage or healing.
    /// </summary>
    public float Multiplier { get; } = multiplier;

}

/// <summary>
/// Fired after a spell reshuffles a duelist's deck. Fires on the duel's actor.
/// </summary>
public sealed class OnDuelistReshuffle(IDuelist duelist) : ImlightEvent {

    /// <summary>
    /// The player or creature whose deck was reshuffled.
    /// </summary>
    public IDuelist Duelist { get; } = duelist;

}
