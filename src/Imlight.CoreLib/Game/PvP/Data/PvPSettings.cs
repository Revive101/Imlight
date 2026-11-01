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
 * Reads the [PvP] section of Imlight.ini.
 * 
 * USAGE EXAMPLE:
 * if (!PvPSettings.IsEnabled) { }
 * 
 * NOTE:
 * The values are read on every call, so a reloaded configuration takes effect without a restart.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using Imlight.Common;

namespace Imlight.CoreLib.Game.PvP.Data;

internal static class PvPSettings {

    public static bool IsEnabled
        => ConfigurationManager.GetValue("PvP.Enabled", true);

    public static byte PracticePlanningSeconds
        => (byte) Math.Clamp(ConfigurationManager.GetValue("PvP.Practice.PlanningSeconds", 20), 1, byte.MaxValue);

    public static bool RankedEnabled
        => ConfigurationManager.GetValue("PvP.RankedEnabled", true);

}
