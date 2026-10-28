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
 * Decides whether a client may attach to a house zone, and to whose house.
 *
 * USAGE EXAMPLE:
 * if (!HouseAttach.TryAuthorize(wizard, zoneName, message.ZoneID, out var house, out var access)) {
 *     // refuse the attach
 * }
 *
 * NOTE:
 * - The client names the zone and the lot in MSG_ATTACH, so neither is trusted: the lot must be a stored house, its
 *   template must have the zone as its lot zone, and HouseAccess must let the character in.
 * - A client that names no lot gets its own house in the zone.
 * - This reads the stored house and never publishes a live state. HousingService counts the viewer in later.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Access;

internal static class HouseAttach {

    /// <summary>
    /// Checks whether a character may attach to a zone.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <param name="zoneName">The zone the client attaches to.</param>
    /// <param name="messageZoneId">The zone id the client sent. It is not used for an ordinary zone.</param>
    /// <param name="house">The stored house when the zone is a house; otherwise <c>null</c>.</param>
    /// <param name="access">The character's access to the house; <see cref="HouseAccessLevel.Denied"/> for an ordinary zone.</param>
    /// <returns>
    /// <c>true</c> for an ordinary zone, and for a house the character may enter. A house zone without a lot is the character's own.
    /// </returns>
    internal static bool TryAuthorize(Wizard wizard, string zoneName, ulong messageZoneId, out House house, out HouseAccessLevel access) {
        house = null;
        access = HouseAccessLevel.Denied;
        if (!HouseZoneIndex.IsHouseZone(zoneName)) {
            return true;
        }

        var lotGid = messageZoneId != 0 ? messageZoneId : HouseLots.TryFindInZone(wizard, zoneName, out _, out var ownLot) ? ownLot : 0;
        var stored = lotGid == 0 ? null : HouseCollection.Load(lotGid);
        if (stored is null || !HouseTemplates.TryGet(stored.TemplateId, out var template)
            || !string.Equals(template.LotZone, zoneName, StringComparison.OrdinalIgnoreCase)) {
            Logger.Warning("Character {CharId} may not attach to house zone {Zone} with lot {LotGid}: no such house there.",
                Logger.Args(wizard.CharId, zoneName, lotGid));

            return false;
        }

        access = HouseAccess.Evaluate(wizard, stored);
        if (access == HouseAccessLevel.Denied) {
            Logger.Warning("Character {CharId} may not enter house {LotGid} in zone {Zone}.",
                Logger.Args(wizard.CharId, lotGid, zoneName));

            return false;
        }

        house = stored;

        return true;
    }

}
