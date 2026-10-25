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
 * Reads one game data file as the PropertyClass type a PvP loader expects.
 * 
 * USAGE EXAMPLE:
 * PvPResourceReader.TryRead(file, data, out TournamentTemplate template);
 * 
 * NOTE:
 * A file of another type, or one that does not deserialize, is logged and skipped.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Imcodec.ObjectProperty;
using Imcodec.Wad;
using Imlight.Common;

namespace Imlight.CoreLib.Game.PvP.Data;

internal static class PvPResourceReader {

    public static bool TryRead<T>(FileEntry file, Memory<byte>? data, out T value) where T : PropertyClass {
        value = null;
        if (data is null) {
            Logger.Error("PvP resource {File} has no data.", Logger.Args(file.FileName));

            return false;
        }

        try {
            var serializer = new BindSerializer();
            if (!serializer.Deserialize<PropertyClass>(data.Value.ToArray(), out var read) || read is null) {
                Logger.Error("Failed to deserialize PvP resource {File}.", Logger.Args(file.FileName));

                return false;
            }

            if (read is not T typed) {
                Logger.Error("PvP resource {File} is a {Actual}, not a {Expected}.",
                    Logger.Args(file.FileName, read.GetType().Name, typeof(T).Name));

                return false;
            }

            value = typed;

            return true;
        } catch (Exception ex) {
            Logger.Error("Failed to read PvP resource {File}: {Exception}", Logger.Args(file.FileName, ex.Message));

            return false;
        }
    }

}
