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

namespace Imlight.PluginApi;

/// <summary>
/// A running duel in a zone, PvE or PvP.
/// </summary>
/// <remarks>
/// An IDuel covers one fight. The next fight on the same sigil is a new IDuel with the same <see cref="Id"/>.
/// </remarks>
public interface IDuel {

    /// <summary>
    /// The global ID of the duel's sigil.
    /// </summary>
    ulong Id { get; }

    /// <summary>
    /// The zone the duel is in.
    /// </summary>
    IZone Zone { get; }

    /// <summary>
    /// True if this is a PvP match rather than a fight against creatures.
    /// </summary>
    bool IsPvP { get; }

    /// <summary>
    /// The current round, starting at 1. It is 0 until the first round begins.
    /// </summary>
    int Round { get; }

    /// <summary>
    /// True once the duel is over. It never turns false again, and <see cref="Players"/> and
    /// <see cref="Creatures"/> are empty from then on.
    /// </summary>
    bool HasEnded { get; }

    /// <summary>
    /// The players in the duel right now, including any at 0 health, who can still be revived. A player who flees
    /// or forfeits leaves the list. Safe to read from any thread; the list is a snapshot and does not change after
    /// it is returned.
    /// </summary>
    IReadOnlyList<IPlayer> Players { get; }

    /// <summary>
    /// The creatures in the duel right now, on either side, including summoned minions. A creature leaves the list
    /// once <see cref="ICreature.IsDead"/> is true. Safe to read from any thread; the list is a snapshot and does
    /// not change after it is returned.
    /// </summary>
    IReadOnlyList<ICreature> Creatures { get; }

}
