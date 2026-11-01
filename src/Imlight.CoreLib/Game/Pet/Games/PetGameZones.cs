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
 * PET GAME ZONES
 * ========================================================================
 *
 * PURPOSE:
 * Finds the zone each pet game plays in. A game's zone holds an object whose template carries a
 * PetGameBehaviorTemplate naming the game; a game with one zone per track has several.
 *
 * USAGE EXAMPLE:
 * PetGamesService asks for the zone of a game and track when the player joins it.
 *
 * NOTE:
 * The candidate archives come from the patch server's file list: first those named after the game,
 * then every archive of the worlds that hold the other games' zones. A game's zones are sorted by
 * name, one per track. Results are kept for the life of the server.
 *
 * TODO:
 * - Do the per-track zones always sort in track order?
 *
 * Created by: Jay with Claude Code (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using PetGameConfig = Imlight.CoreLib.Game.Pet.Config.PetGameConfig;

namespace Imlight.CoreLib.Game.Pet.Games;

/// <summary>
/// The zones the pet games play in, found from the zone data.
/// </summary>
internal static class PetGameZones {

    private const string ZONE_DATA_FILE_NAME = "gamedata.bin";
    private const string ARCHIVE_EXTENSION = ".wad";
    private const char ARCHIVE_WORLD_SEPARATOR = '-';

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> s_zonesByGame = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the zone a game plays in on a track.
    /// </summary>
    /// <param name="gameName">The game name as it appears in PetGames.xml.</param>
    /// <param name="trackIndex">The chosen track.</param>
    /// <param name="zone">The zone name, null if none was found.</param>
    /// <returns>Whether a zone was found.</returns>
    internal static bool TryGetZone(string gameName, int trackIndex, out string zone) {
        var zones = s_zonesByGame.GetOrAdd(gameName, FindZones);
        zone = zones.Count switch {
            0 => null,
            1 => zones[0],
            _ => trackIndex >= 0 && trackIndex < zones.Count ? zones[trackIndex] : null,
        };

        return zone is not null;
    }

    private static IReadOnlyList<string> FindZones(string gameName) {
        var archives = GetArchiveNames();
        var namedAfterGame = archives.Where(archive => archive.Contains(gameName, StringComparison.OrdinalIgnoreCase)).ToList();
        var zones = FindControllerZones(namedAfterGame, gameName);
        if (zones.Count == 0) {
            // The zone of this game is named apart from it, so it is looked for beside the zones of the other games.
            var worlds = GetPetGameWorlds(archives);
            var beside = archives.Where(archive => worlds.Contains(GetWorld(archive)) && !namedAfterGame.Contains(archive));
            zones = FindControllerZones(beside, gameName);
        }

        if (zones.Count == 0) {
            Logger.Warning("No zone holds the pet game {Game}.", Logger.Args(gameName));
        } else {
            Logger.Debug("The pet game {Game} plays in {Count} zone(s): {Zones}.", Logger.Args(gameName, zones.Count, string.Join(", ", zones)));
        }

        return zones;
    }

    private static List<string> GetArchiveNames() {
        var files = PatchServerFascade.GetLatestFileList()?.Files;
        if (files is null) {
            Logger.Warning("The patch server gave no file list, so no pet game zone can be found.");

            return [];
        }

        return [.. files
            .Select(file => file.SourceFileName)
            .Where(name => name?.EndsWith(ARCHIVE_EXTENSION, StringComparison.OrdinalIgnoreCase) == true)
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static HashSet<string> GetPetGameWorlds(List<string> archives) {
        var gameNames = PetGameConfig.Games.Select(game => (string) game.m_name).Where(name => !string.IsNullOrEmpty(name)).ToList();

        return archives
            .Where(archive => gameNames.Any(name => archive.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(GetWorld)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetWorld(string archive) {
        var separator = archive.IndexOf(ARCHIVE_WORLD_SEPARATOR);

        return separator > 0 ? archive[..separator] : archive;
    }

    private static List<string> FindControllerZones(IEnumerable<string> archives, string gameName) {
        var zones = new List<string>();
        foreach (var archive in archives) {
            var zoneData = LoadZoneData(archive);
            if (zoneData?.m_objectList?.Any(objectInfo => RunsGame(objectInfo, gameName)) == true) {
                zones.Add(zoneData.m_zoneName);
            }
        }

        zones.Sort(StringComparer.Ordinal);

        return zones;
    }

    private static WizZoneData LoadZoneData(string archive) {
        try {
            return ResourceManager.LoadDeserializedFile<WizZoneData>(archive, ZONE_DATA_FILE_NAME);
        } catch (Exception ex) {
            Logger.Debug("Could not read the zone data of {Archive}: {Error}", Logger.Args(archive, ex.Message));

            return null;
        }
    }

    private static bool RunsGame(CoreObjectInfo objectInfo, string gameName)
        => objectInfo is not null
            && CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID) is GameObjectTemplate template
            && template.m_behaviors?.OfType<PetGameBehaviorTemplate>()
                .Any(behavior => string.Equals(behavior.m_gameName, gameName, StringComparison.OrdinalIgnoreCase)) == true;

}
