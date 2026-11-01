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

using System.Numerics;

namespace Imlight.PluginApi;

/// <summary>
/// Anything that can fight in a duel: an <see cref="IPlayer"/> or an <see cref="ICreature"/>.
/// </summary>
/// <remarks>
/// Pattern match to get at what only one of them has, e.g. <c>e.Duelist is IPlayer player</c>.
/// </remarks>
public interface IDuelist {

    /// <summary>
    /// A player's display name or a creature's internal name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The duel the duelist is in, or null if they are not in one.
    /// </summary>
    IDuel? Duel { get; }

    /// <summary>
    /// The duelist's position in their zone.
    /// </summary>
    Vector3 Location { get; }

    /// <summary>
    /// The direction the duelist is facing, in radians.
    /// </summary>
    float Orientation { get; }

    /// <summary>
    /// The duelist's level.
    /// </summary>
    int Level { get; }

    /// <summary>
    /// The duelist's school of magic, or <see cref="PluginApi.School.None"/> for a creature that has none.
    /// </summary>
    School School { get; }

    /// <summary>
    /// The duelist's current health. In a duel it changes as they take damage and heal.
    /// </summary>
    int Health { get; }

    /// <summary>
    /// The duelist's maximum health.
    /// </summary>
    int MaxHealth { get; }

}
