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

namespace Imlight.PluginApi.Events;

/// <summary>
/// Fired when a creature has been added to its zone, including a minion summoned in a duel.
/// <see cref="IZone.Creatures"/> already includes it. Fires on the creature's actor.
/// </summary>
public sealed class OnCreatureSpawn(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that spawned.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a creature has left its zone for any reason: after it dies, when a plugin or the game despawns it,
/// when a duel it is in ends, or when its zone unloads. Every creature that fired <see cref="OnCreatureSpawn"/>
/// fires this once. Fires on the creature's actor, which has already stopped, so calls on the creature do nothing.
/// </summary>
public sealed class OnCreatureDespawn(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that left. <see cref="ICreature.IsDead"/> is true and <see cref="IZone.Creatures"/> no longer
    /// includes it.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a creature loses health in a duel, as the server resolves the round and before the client plays it.
/// Hits fully absorbed by wards or resistances don't fire it. Fires on the duel's actor, not the creature's.
/// </summary>
public sealed class OnCreatureDamage(ICreature creature, int damage) : ImlightEvent {

    /// <summary>
    /// The creature that was damaged. <see cref="ICreature.Health"/> is already reduced.
    /// </summary>
    public ICreature Creature { get; } = creature;

    /// <summary>
    /// The health the creature lost, never more than it had.
    /// </summary>
    public int Damage { get; } = damage;

}

/// <summary>
/// Fired when a defeated creature is removed from its duel at the end of a round, or a creature in a duel is
/// despawned. <see cref="OnCreatureDespawn"/> follows once it leaves the zone. Creatures that are removed alive
/// don't fire it. Fires on the creature's actor.
/// </summary>
public sealed class OnCreatureDeath(ICreature creature) : ImlightEvent {

    /// <summary>
    /// The creature that died. <see cref="ICreature.IsDead"/> is true and <see cref="ICreature.Health"/> is zero.
    /// </summary>
    public ICreature Creature { get; } = creature;

}

/// <summary>
/// Fired when a creature's or minion's queued spell is about to be cast in a duel, before it is rolled for a fizzle.
/// Passes, and turns lost to a stun or a dead target, don't fire it. Cancelling it turns the cast into a pass.
/// Fires on the duel's actor, not the creature's.
/// </summary>
public sealed class OnCreatureSpellCast(ICreature creature, uint spellTemplateId, string spellName, string school,
    int pipCost) : ImlightEvent, ICancellable {

    /// <summary>
    /// The creature that is casting.
    /// </summary>
    public ICreature Creature { get; } = creature;

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
