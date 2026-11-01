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
using System.Numerics;

namespace Imlight.PluginApi;

/// <summary>
/// A combat creature in a zone, whether a mob from a spawn path, one spawned by a trigger or quest,
/// or a minion summoned in combat. Friendly NPCs that cannot be fought are not creatures.
/// </summary>
/// <remarks>
/// An ICreature lives until the creature dies or despawns; a respawn is a new creature with a new
/// <see cref="GlobalId"/>.
/// </remarks>
public interface ICreature {

    /// <summary>
    /// The creature's global ID, unique while it is spawned.
    /// </summary>
    ulong GlobalId { get; }

    /// <summary>
    /// The ID of the template the creature was made from. Every spawn of the same kind of creature shares it.
    /// </summary>
    ulong TemplateId { get; }

    /// <summary>
    /// The creature's internal name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The zone the creature is in.
    /// </summary>
    IZone Zone { get; }

    /// <summary>
    /// The creature's position in its zone.
    /// </summary>
    Vector3 Location { get; }

    /// <summary>
    /// The direction the creature is facing, in radians.
    /// </summary>
    float Orientation { get; }

    /// <summary>
    /// The creature's level.
    /// </summary>
    int Level { get; }

    /// <summary>
    /// The creature's school of magic, or <see cref="PluginApi.School.None"/> if it has none.
    /// </summary>
    School School { get; }

    /// <summary>
    /// The creature's current health. In a duel it changes as the creature takes damage and heals.
    /// </summary>
    int Health { get; }

    /// <summary>
    /// The creature's maximum health.
    /// </summary>
    int MaxHealth { get; }

    /// <summary>
    /// True if the creature is a boss.
    /// </summary>
    bool IsBoss { get; }

    /// <summary>
    /// True if the creature is a minion summoned in a duel. A minion exists only in that duel.
    /// </summary>
    bool IsMinion { get; }

    /// <summary>
    /// True while the creature is fighting in a duel. A creature that joins a duel stays in it until it dies or despawns.
    /// </summary>
    bool InCombat { get; }

    /// <summary>
    /// True once the creature has died, despawned or been removed by its duel ending. It never turns false again.
    /// </summary>
    /// <remarks>
    /// A creature at 0 health in a duel is not dead until the end of the round, when the duel removes it.
    /// </remarks>
    bool IsDead { get; }

    /// <summary>
    /// The chance, from 0 to 1, that the creature picks the strongest spell in its hand each round.
    /// </summary>
    float Intelligence { get; }

    /// <summary>
    /// The chance, from 0 to 1, that the creature attacks each round rather than preparing.
    /// </summary>
    float Aggressiveness { get; }

    /// <summary>
    /// The chance, from 0 to 1, that the creature prepares on itself rather than on a teammate.
    /// </summary>
    float Selfishness { get; }

    /// <summary>
    /// How close a player must come for the creature to start a duel with them.
    /// </summary>
    float Proximity { get; }

    /// <summary>
    /// The template ID of each spell in the creature's deck, listed once however many copies it has.
    /// The list does not change.
    /// </summary>
    IReadOnlyList<uint> Spells { get; }

    // Every write below is applied by the creature's own actor after it returns, so the matching
    // property may not change right away. Calls on a creature that has died or despawned do nothing.

    /// <summary>
    /// Sets the creature's health, clamped between 1 and <see cref="MaxHealth"/>. Use <see cref="Despawn"/> to kill it.
    /// </summary>
    /// <remarks>
    /// In a duel, players see the new health at the start of the next round.
    /// </remarks>
    void SetHealth(int health);

    /// <summary>
    /// Sets the creature's maximum health. Its health is lowered to the new max if it was above it.
    /// </summary>
    /// <remarks>
    /// In a duel, players see the new health at the start of the next round.
    /// </remarks>
    /// <exception cref="System.ArgumentOutOfRangeException">The max is not positive.</exception>
    void SetMaxHealth(int maxHealth);

    /// <summary>
    /// Sets the creature's <see cref="Intelligence"/>. In a duel it applies from the creature's next round.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The chance is not between 0 and 1.</exception>
    void SetIntelligence(float intelligence);

    /// <summary>
    /// Sets the creature's <see cref="Aggressiveness"/>. In a duel it applies from the creature's next round.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The chance is not between 0 and 1.</exception>
    void SetAggressiveness(float aggressiveness);

    /// <summary>
    /// Sets the creature's <see cref="Selfishness"/>. In a duel it applies from the creature's next round.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The chance is not between 0 and 1.</exception>
    void SetSelfishness(float selfishness);

    /// <summary>
    /// Sets the creature's <see cref="Proximity"/>. A player already inside the new range is noticed the next
    /// time they move.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">The proximity is negative.</exception>
    void SetProximity(float proximity);

    /// <summary>
    /// Stops the creature walking its path once it reaches the point it is walking to. Creatures that do not
    /// walk a path, such as minions, ignore it.
    /// </summary>
    void StopMoving();

    /// <summary>
    /// Lets a creature stopped by <see cref="StopMoving"/> walk its path again. A creature in a duel stays put
    /// regardless.
    /// </summary>
    void ResumeMoving();

    /// <summary>
    /// Removes the creature from the zone. Its spawn point can spawn a new one later, except in an instance.
    /// </summary>
    /// <remarks>
    /// A creature in a duel is defeated instead, at the end of the current round, the same as if a player had
    /// killed it, so players who win still get their rewards.
    /// </remarks>
    void Despawn();

}
