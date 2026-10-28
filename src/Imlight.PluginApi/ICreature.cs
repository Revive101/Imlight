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
 * Last Updated: 10/27/2026
 */

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

}
