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

using System.Collections.Generic;

namespace Imlight.PluginApi;

/// <summary>
/// A running zone. Instanced zones (dungeons) have one IZone per instance.
/// </summary>
/// <remarks>
/// An IZone lives as long as the zone is loaded, and instances are dropped once they sit empty.
/// Store <see cref="Name"/> rather than the IZone itself.
/// </remarks>
public interface IZone {

    /// <summary>
    /// The internal name of the zone, e.g. <c>WizardCity/WC_Hub</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// True if this is a private instance, such as a dungeon, rather than a shared zone.
    /// </summary>
    bool IsInstance { get; }

    /// <summary>
    /// The players in the zone right now. Safe to read from any thread; the list is a snapshot
    /// and does not change after it is returned.
    /// </summary>
    IReadOnlyList<IPlayer> Players { get; }

    /// <summary>
    /// The creatures spawned in the zone right now. Safe to read from any thread; the list is a snapshot
    /// and does not change after it is returned.
    /// </summary>
    IReadOnlyList<ICreature> Creatures { get; }

    /// <summary>
    /// The duels running in the zone right now. Safe to read from any thread; the list is a snapshot
    /// and does not change after it is returned.
    /// </summary>
    IReadOnlyList<IDuel> Duels { get; }

}
