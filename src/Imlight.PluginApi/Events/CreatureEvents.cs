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
