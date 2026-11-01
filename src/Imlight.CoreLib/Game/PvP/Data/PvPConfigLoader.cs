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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * Loads the PvP tournament config from the game data: the active season number, the forfeit and observer
 * timeouts and the tournament name lists.
 * 
 * USAGE EXAMPLE:
 * if (PvPConfigLoader.TryGetConfig(out var config)) { var forfeit = config.m_forfeitTimeout; }
 * 
 * NOTE:
 * The tournament schedule and hosting fields name files that are not in the game data.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class PvPConfigLoader : RootSingleResourceSingleton<PvPConfigLoader> {

    protected override string ResourceName => "PvPTournamentConfig.xml";

    private static TournamentConfig s_config;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize(Stream.ToArray(), out TournamentConfig config)) {
            Logger.Error("Failed to deserialize {Resource}.", Logger.Args(ResourceName));

            return;
        }

        s_config = config;

        Logger.Information("Loaded the PvP tournament config (active season {Season}).", Logger.Args(config.m_activeSeason));
    }

    internal static bool TryGetConfig(out TournamentConfig config) {
        config = s_config;

        return config is not null;
    }

}
