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
 * HOUSING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Reads the [Housing] section of Imlight.ini.
 * 
 * USAGE EXAMPLE:
 * var timeout = HousingSettings.DeedZoneTimeout;
 * 
 * NOTE:
 * - DeedZoneTimeoutSeconds: how long to wait for the client to confirm that a house zone archive is ready.
 * - StarterDeeds: the templates every character is given on the first Home click, as a comma-separated list.
 * - AtticLimit: how many items an attic holds.
 * - IslandLimit: how many deeds a character may hold. The client refuses to buy a deed past it.
 * - TextureProxyTemplate: the template of the object that holds a house's wallpaper and flooring; empty turns textures off.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Housing;

/// <summary>
/// The housing settings.
/// </summary>
internal static class HousingSettings {

    private const string DEED_ZONE_TIMEOUT_KEY = "Housing.DeedZoneTimeoutSeconds";
    private const string STARTER_DEEDS_KEY = "Housing.StarterDeeds";
    private const string ATTIC_LIMIT_KEY = "Housing.AtticLimit";
    private const string ISLAND_LIMIT_KEY = "Housing.IslandLimit";
    private const string TEXTURE_PROXY_TEMPLATE_KEY = "Housing.TextureProxyTemplate";
    private const int DEFAULT_DEED_ZONE_TIMEOUT_SECONDS = 90;
    private const int DEFAULT_ATTIC_LIMIT = 100;
    private const int DEFAULT_ISLAND_LIMIT = 30;

    /// <summary>
    /// How long to wait for the client to confirm that a house zone archive is ready:
    /// <c>Housing.DeedZoneTimeoutSeconds</c>, or 90 seconds when it is not a positive number.
    /// </summary>
    internal static TimeSpan DeedZoneTimeout {
        get {
            var configured = ConfigurationManager.Settings[DEED_ZONE_TIMEOUT_KEY].AsInt();

            return TimeSpan.FromSeconds(configured > 0 ? configured : DEFAULT_DEED_ZONE_TIMEOUT_SECONDS);
        }
    }

    /// <summary>
    /// How many items an attic holds: <c>Housing.AtticLimit</c>, or 100 when it is not a positive number.
    /// </summary>
    internal static int AtticLimit {
        get {
            var configured = ConfigurationManager.Settings[ATTIC_LIMIT_KEY].AsInt();

            return configured > 0 ? configured : DEFAULT_ATTIC_LIMIT;
        }
    }

    /// <summary>
    /// How many deeds a character may hold: <c>Housing.IslandLimit</c>, or 30 when it is not a positive number.
    /// The client reads it as the character's maximum number of islands.
    /// </summary>
    internal static int IslandLimit {
        get {
            var configured = ConfigurationManager.Settings[ISLAND_LIMIT_KEY].AsInt();

            return configured > 0 ? configured : DEFAULT_ISLAND_LIMIT;
        }
    }

    /// <summary>
    /// The templates of the houses every character is given: <c>Housing.StarterDeeds</c>, in order.
    /// An entry that is not a template id is logged and left out.
    /// </summary>
    internal static IReadOnlyList<uint> StarterDeeds {
        get {
            var starters = new List<uint>();
            foreach (var entry in ConfigurationManager.Settings[STARTER_DEEDS_KEY].AsList()) {
                if (TryParseTemplateId(entry, out var templateId)) {
                    starters.Add(templateId);
                } else {
                    Logger.Warning("{Key} has an entry that is not a template id: {Entry}", Logger.Args(STARTER_DEEDS_KEY, entry));
                }
            }

            return starters;
        }
    }

    /// <summary>
    /// The template of the object that holds a house's wallpaper and flooring: <c>Housing.TextureProxyTemplate</c>, or 0
    /// when it is not a template id.
    /// </summary>
    internal static uint TextureProxyTemplate
        => TryParseTemplateId(ConfigurationManager.Settings[TEXTURE_PROXY_TEMPLATE_KEY].AsString(), out var templateId) ? templateId : 0;

    private static bool TryParseTemplateId(string entry, out uint templateId)
        => uint.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out templateId) && templateId != 0;

}
