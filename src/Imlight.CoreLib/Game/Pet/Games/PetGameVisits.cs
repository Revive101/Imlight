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
 * ========================================================================
 * PET GAME VISITS
 * ========================================================================
 *
 * PURPOSE:
 * Remembers which pet game a player went to play, in which zone, and where to send them back.
 *
 * USAGE EXAMPLE:
 * PetGamesService adds a visit on join, before the zone transfer that starts a new session, and the
 * new session reads it once the player arrives in the game's zone.
 *
 * NOTE:
 * A visit ends when the player is sent back, or arrives in any other zone.
 *
 * TODO:
 * 
 * Created by: Jay with Claude Code (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Concurrent;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// A player's trip to a pet game's zone.
/// </summary>
/// <param name="GameName">The game name as it appears in PetGames.xml.</param>
/// <param name="TrackIndex">The chosen track.</param>
/// <param name="Zone">The game's zone.</param>
/// <param name="ReturnZone">The zone the player joined from.</param>
/// <param name="ReturnLocation">Where the player stood, as a compact location string.</param>
internal sealed record PetGameVisit(string GameName, int TrackIndex, string Zone, string ReturnZone, string ReturnLocation) {

    /// <summary>
    /// Whether the game was opened in the zone. A later arrival only sends the player back.
    /// </summary>
    internal bool Opened { get; set; }

    /// <summary>
    /// Checks whether a game name is this visit's game.
    /// </summary>
    /// <param name="gameName">The game name.</param>
    /// <returns>True if it names this visit's game.</returns>
    internal bool IsGame(string gameName)
        => string.Equals(GameName, gameName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether a zone is this visit's game zone.
    /// </summary>
    /// <param name="zone">The zone name.</param>
    /// <returns>True if it is the game's zone.</returns>
    internal bool IsZone(string zone)
        => string.Equals(Zone, zone, StringComparison.OrdinalIgnoreCase);

}

/// <summary>
/// The pet game visits in progress, by character id.
/// </summary>
internal static class PetGameVisits {

    private static readonly ConcurrentDictionary<ulong, PetGameVisit> s_visits = new();

    /// <summary>
    /// Starts a visit, replacing any earlier one of the character.
    /// </summary>
    /// <param name="charId">The character id.</param>
    /// <param name="visit">The visit.</param>
    internal static void Begin(ulong charId, PetGameVisit visit)
        => s_visits[charId] = visit;

    /// <summary>
    /// Gets the character's visit.
    /// </summary>
    /// <param name="charId">The character id.</param>
    /// <param name="visit">The visit, null if there is none.</param>
    /// <returns>Whether the character is on a visit.</returns>
    internal static bool TryGet(ulong charId, out PetGameVisit visit)
        => s_visits.TryGetValue(charId, out visit);

    /// <summary>
    /// Checks whether the character is on a visit.
    /// </summary>
    /// <param name="charId">The character id.</param>
    /// <returns>True if the character is on a visit.</returns>
    internal static bool Contains(ulong charId)
        => s_visits.ContainsKey(charId);

    /// <summary>
    /// Ends the character's visit.
    /// </summary>
    /// <param name="charId">The character id.</param>
    internal static void End(ulong charId)
        => s_visits.TryRemove(charId, out _);

}
