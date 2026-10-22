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
 * Describes how a zone transfer into a house asks the client to load the house zone, and reads the client's answer.
 * Every way into a house sets one of these on its MSG_ZONETRANSFER, so the handshake always has something to wait for.
 *
 * USAGE EXAMPLE:
 * var entry = HouseEntry.ForTeleport(lotGid, ownerCharId, template);
 * Teleport(zone, houseEntry: entry);
 * // ZoneService sends entry.CreateRequest(location) and waits for an echo that entry.Read accepts.
 *
 * NOTE:
 * - Deed: the item the client opens the zone for (Home). Teleport: only the house template (a visit, or a trigger that
 *   teleports into a house). Door: the zone object that carries the TeleportProximityBehavior.
 * - The client answers with the same message type. A deed echo says ready (0) or still patching (1), a door echo says
 *   ready (0) or that it cannot find the deed template (1), and a teleport echo comes only once the zone is ready.
 * - The lot is the zone instance id the client attaches to. The owner is whose instance container holds the zone.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System.Collections.Generic;
using System.Globalization;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Housing.Houses;

namespace Imlight.CoreLib.Game.Housing.Access;

/// <summary>
/// How the client is asked to load a house zone.
/// </summary>
public enum HouseEntryKind {
    Deed,
    Teleport,
    Door,
}

/// <summary>
/// What an echo of the client's means for a pending entry.
/// </summary>
internal enum HouseEchoStatus {
    NotForThisEntry,
    Ready,
    Patching,
    Refused,
}

/// <summary>
/// What the zone transfer into a house needs: how to ask the client, which lot and owner, and the new items the
/// client has not seen.
/// </summary>
/// <param name="Kind">How the client is asked to load the zone.</param>
/// <param name="LotGid">The lot, which is the zone instance id.</param>
/// <param name="Zone">The lot zone.</param>
/// <param name="OwnerCharId">The owner of the house, whose instance container holds the zone.</param>
/// <param name="TemplateId">The template of the house, which the client resolves the zone from.</param>
/// <param name="ObjectGid">The deed item for <see cref="HouseEntryKind.Deed"/>, the door for <see cref="HouseEntryKind.Door"/>, else 0.</param>
/// <param name="NewItemGids">The global ids of the items the client has to be told about first.</param>
public sealed record HouseEntry(
    HouseEntryKind Kind,
    ulong LotGid,
    string Zone,
    ulong OwnerCharId,
    uint TemplateId,
    ulong ObjectGid,
    IReadOnlyList<ulong> NewItemGids) {

    private const int READY_STATUS = 0;
    private const int PATCHING_STATUS = 1;

    /// <summary>
    /// Makes the entry for the owner's Home: the client opens the zone for the deed item.
    /// </summary>
    internal static HouseEntry ForDeed(ulong lotGid,
                                       ulong ownerCharId,
                                       HouseTemplateInfo template,
                                       ulong deedGid,
                                       IReadOnlyList<ulong> newItemGids)
        => new(HouseEntryKind.Deed, lotGid, template.LotZone, ownerCharId, template.TemplateId, deedGid, newItemGids);

    /// <summary>
    /// Makes the entry for a player who has no deed item for the house: the client opens the zone for the template.
    /// </summary>
    internal static HouseEntry ForTeleport(ulong lotGid,
                                           ulong ownerCharId,
                                           HouseTemplateInfo template,
                                           IReadOnlyList<ulong> newItemGids = null)
        => new(HouseEntryKind.Teleport, lotGid, template.LotZone, ownerCharId, template.TemplateId, 0, newItemGids ?? []);

    /// <summary>
    /// Makes the entry for a door: the client opens the zone of the house on the other side.
    /// </summary>
    internal static HouseEntry ForDoor(ulong lotGid,
                                       ulong ownerCharId,
                                       HouseTemplateInfo template,
                                       ulong doorGid,
                                       IReadOnlyList<ulong> newItemGids = null)
        => new(HouseEntryKind.Door, lotGid, template.LotZone, ownerCharId, template.TemplateId, doorGid, newItemGids ?? []);

    /// <summary>
    /// Builds the message that asks the client to load the zone.
    /// </summary>
    /// <param name="location">The location the player arrives at, which a teleport request carries.</param>
    /// <returns>The request for <see cref="Kind"/>.</returns>
    internal IMessage CreateRequest(string location) => Kind switch {
        HouseEntryKind.Deed => new WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTDEEDZONE {
            GlobalID = ObjectGid,
            Status = READY_STATUS,
            Teleport = 0,
            Arguments = string.Create(CultureInfo.InvariantCulture, $"CLUSTER={LotGid}"),
        },
        HouseEntryKind.Teleport => new WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTHOUSINGZONETELEPORT {
            TemplateGID = TemplateId,
            Location = location ?? string.Empty,
        },
        _ => new WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTHOUSINGZONE {
            GlobalID = ObjectGid,
            Status = READY_STATUS,
            TemplateGID = TemplateId,
        },
    };

    /// <summary>
    /// Reads the client's echo of a deed request.
    /// </summary>
    internal HouseEchoStatus Read(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTDEEDZONE echo) {
        if (Kind != HouseEntryKind.Deed || echo.GlobalID != ObjectGid) {
            return HouseEchoStatus.NotForThisEntry;
        }

        return echo.Status switch {
            READY_STATUS => HouseEchoStatus.Ready,
            PATCHING_STATUS => HouseEchoStatus.Patching,
            _ => HouseEchoStatus.Refused,
        };
    }

    /// <summary>
    /// Reads the client's echo of a teleport request. The client sends it once the zone is ready.
    /// </summary>
    internal HouseEchoStatus Read(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTHOUSINGZONETELEPORT echo)
        => Kind == HouseEntryKind.Teleport && echo.TemplateGID == TemplateId ? HouseEchoStatus.Ready : HouseEchoStatus.NotForThisEntry;

    /// <summary>
    /// Reads the client's echo of a door request.
    /// </summary>
    internal HouseEchoStatus Read(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTHOUSINGZONE echo) {
        if (Kind != HouseEntryKind.Door || echo.GlobalID != ObjectGid) {
            return HouseEchoStatus.NotForThisEntry;
        }

        return echo.Status == READY_STATUS ? HouseEchoStatus.Ready : HouseEchoStatus.Refused;
    }

}
