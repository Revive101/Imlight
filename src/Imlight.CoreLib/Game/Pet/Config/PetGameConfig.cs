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
 * PET GAME CONFIGURATION
 * ========================================================================
 *
 * PURPOSE:
 * Loads the pet game list from the root archive: the games, the energy cost of each by pet level,
 * and the stat changes and score factors of each track choice.
 *
 * USAGE EXAMPLE:
 * if (PetGameConfig.TryGetEnergyCost(gameName, pet.m_level, out var cost)) { ... }
 *
 * NOTE:
 * Energy lookups mirror PetGameInfo::GetEnergyCost in the client: the cost of the highest rank at or
 * below the pet's level.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The pet games and their energy costs and track choices, as defined in PetGames.xml.
/// </summary>
internal sealed class PetGameConfig : RootSingleResourceSingleton<PetGameConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "PetGames.xml";

    private static List<PetGameInfo> s_games = [];
    private static Dictionary<string, PetGameInfo> s_gamesByName = new(StringComparer.Ordinal);

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<Imcodec.ObjectProperty.TypeCache.PetGameConfig>(Stream.ToArray(), out var config)) {
            Logger.Error("Failed to deserialize PetGames.xml");

            return;
        }

        var byName = new Dictionary<string, PetGameInfo>(StringComparer.Ordinal);
        foreach (var game in config.m_games) {
            var name = game.m_name.ToString();
            if (name.Length == 0 || !byName.TryAdd(name, game)) {
                Logger.Error("PetGames.xml has a game with the empty or duplicate name {Name}.", 
                    Logger.Args(name));
            }

            if (!HasAscendingRanks(game)) {
                Logger.Error("PetGames.xml: the energy costs of {Name} do not have strictly ascending ranks.", 
                    Logger.Args(name));
            }
        }

        s_games = config.m_games;
        s_gamesByName = byName;
        Logger.Information("Loaded {Count} pet games.", 
            Logger.Args(s_games.Count));

        DisposeStream();
    }

    private static bool HasAscendingRanks(PetGameInfo game) {
        var costs = game.m_energyCosts;
        for (var i = 1; i < costs.Count; i++) {
            if (costs[i].m_rank <= costs[i - 1].m_rank) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Every game in file order.
    /// </summary>
    internal static IReadOnlyList<PetGameInfo> Games => s_games;

    /// <summary>
    /// Finds a game by its exact name.
    /// </summary>
    /// <param name="name">The game name as it appears in the file.</param>
    /// <returns>The game, or null if no game has that name.</returns>
    internal static PetGameInfo FindGame(string name)
        => name is not null && s_gamesByName.TryGetValue(name, out var game) ? game : null;

    /// <summary>
    /// Looks up what a game costs a pet of the given level.
    /// </summary>
    /// <param name="gameName">The game name as it appears in the file.</param>
    /// <param name="petLevel">The pet's level.</param>
    /// <param name="cost">The energy cost.</param>
    /// <returns>False if the game is unknown or has no cost at or below that level.</returns>
    internal static bool TryGetEnergyCost(string gameName, int petLevel, out int cost) {
        cost = 0;
        var game = FindGame(gameName);
        if (game is null) {
            return false;
        }

        var found = false;
        foreach (var entry in game.m_energyCosts) {
            if (entry.m_rank <= petLevel) {
                cost = entry.m_cost;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Gets the stat changes and score factors of one of a game's track choices.
    /// </summary>
    /// <param name="gameName">The game name as it appears in the file.</param>
    /// <param name="trackIndex">The index of the track choice.</param>
    /// <returns>The track choice, or null if the game or the index does not exist.</returns>
    internal static PetStatModificationSet GetTrackChoice(string gameName, int trackIndex) {
        var choices = FindGame(gameName)?.m_trackChoices;
        if (choices is null || trackIndex < 0 || trackIndex >= choices.Count) {
            return null;
        }

        return choices[trackIndex];
    }

    public void DisposeStream()
        => base.Stream.Dispose();

}
