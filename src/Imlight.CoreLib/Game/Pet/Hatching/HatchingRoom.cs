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
 * HATCHING ROOM
 * ========================================================================
 *
 * PURPOSE:
 * One player's side of a visit to the hatching room: where to go back to, the partner, the pets each
 * side picked, Hatch or Lend, the ready boxes and whether the hatch is done.
 *
 * USAGE EXAMPLE:
 * _room = new HatchingRoom(message.DestinationZone, message.PartnerCharId, message.ReturnZone, message.ReturnLocation);
 *
 * NOTE:
 * Lives in the player's HatchingService. The partner's service keeps its own and they trade changes.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Pet.Hatching;

/// <summary>
/// The state of one player's visit to the hatching room.
/// </summary>
internal sealed class HatchingRoom(string zone, ulong partnerCharId, string returnZone, string returnLocation) {

    internal const int OWN_PET_PICK_SIDE = 0;
    internal const int SOLO_SECOND_PET_PICK_SIDE = 1;

    internal string Zone { get; } = zone;
    internal string ReturnZone { get; } = returnZone;
    internal string ReturnLocation { get; } = returnLocation;
    internal ulong PartnerCharId { get; private set; } = partnerCharId;
    internal IActorRef PartnerSession { get; set; }
    internal List<WizClientObjectItem> PartnerPets { get; set; }
    internal string GameName { get; set; }
    internal bool Attached { get; set; }
    internal bool WindowOpened { get; set; }
    internal bool ClientReady { get; set; }
    internal bool Started { get; set; }
    internal bool PetsSentToPartner { get; set; }
    /// <summary>
    /// The global ids of the pets the player picked, by pick side.
    /// </summary>
    internal ulong[] Picks { get; } = new ulong[2];
    internal ulong PartnerPickId { get; set; }
    /// <summary>
    /// True if the player picked Lend, false for Hatch, null before a pick.
    /// </summary>
    internal bool? Lends { get; set; }
    internal bool? PartnerLends { get; set; }
    internal bool Ready { get; set; }
    internal bool PartnerReady { get; set; }
    internal bool Done { get; set; }
    internal bool IsDuo => PartnerCharId != 0;
    internal bool ChoicesAgree
        => Lends is not null && PartnerLends is not null && !(Lends.Value && PartnerLends.Value);

    internal void HatchAlone() {
        PartnerCharId = 0;
        PartnerSession = null;
        PartnerPets = null;
    }

}
