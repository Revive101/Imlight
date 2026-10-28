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
 * INTERACT MINIGAME SIGIL COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Makes a minigame sigil that names its own destination zone a press-X pad with one seat per player
 * circle. Each player who presses X takes the first free circle and starts a countdown in their
 * session; players on the same sigil go to the first player's private instance of the zone.
 *
 * USAGE EXAMPLE:
 * Attaches to countdown sigil objects that the dungeon sigil component does not take; a sigil whose
 * data names no destination zone stays inert.
 *
 * NOTE:
 * The seated players' sessions run the countdown and the transfer (ZoneService). A second player
 * restarts the first player's countdown; when one steps off, the other goes alone.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractMinigameSigilComponent(ZoneEntity entity)
    : InteractSigilComponentBase(entity), IComponentFactory, IWithTimers {

    private const string DEFAULT_ARRIVAL_LOCATION = "Start";
    private const string RELIGHT_TIMER_KEY = "sigil-relight";

    private static readonly TimeSpan s_relightDelay = TimeSpan.FromSeconds(2);

    public ITimerScheduler Timers { get; set; }

    private SigilSeat[] _seats;

    private string DestinationZone
        => SigilInfo?.m_successZone.ToString() ?? "";

    protected override bool IsAvailable
        => !string.IsNullOrEmpty(DestinationZone);

    private SigilSeat[] Seats
        => _seats ??= new SigilSeat[CountPlayerCircles()];

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObject
            && !InteractDungeonSigilComponent.ShouldAttachToEntity(template)
            && gameObject.m_behaviors?.Any(behavior => behavior is CountdownBehaviorTemplate) == true;

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        var seat = Array.FindIndex(Seats, seated => seated?.Session.Equals(playerActor) == true);
        if (seat < 0) {
            return;
        }

        Unseat(seat, tellPartner: true);
    }

    public override void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (!IsAvailable || Seats.Any(seated => seated?.CharId == playerCharacter.CharId)) {
            return;
        }

        var seat = Array.FindIndex(Seats, seated => seated is null);
        if (seat < 0) {
            return;
        }

        var partner = Seats.FirstOrDefault(seated => seated is not null);
        var roomOwner = partner?.CharId ?? playerCharacter.CharId;
        Seats[seat] = new SigilSeat(playerCharacter.CharId, playerActor);

        playerActor.Tell(CreateEntry(seat, roomOwner, partner?.CharId ?? 0));
        partner?.Session.Tell(new ZONE_102_PROTOCOL.MSG_SIGILPARTNERJOINED {
            PartnerCharId = playerCharacter.CharId,
            RoomOwnerCharId = roomOwner,
        });
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SIGILOCCUPANTLEFT))]
    private void ReceiveOccupantLeft(ZONE_102_PROTOCOL.MSG_SIGILOCCUPANTLEFT message) {
        var seat = Array.FindIndex(Seats, seated => seated?.CharId == message.CharId);
        if (seat < 0) {
            return;
        }

        Unseat(seat, tellPartner: !message.Entered);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SIGILRELIGHT))]
    private void ReceiveRelight(ZONE_102_PROTOCOL.MSG_SIGILRELIGHT message)
        => Entity.ChangeState(LIT_STATE);

    private void Unseat(int seat, bool tellPartner) {
        Seats[seat] = null;
        if (tellPartner) {
            foreach (var seated in Seats.Where(seated => seated is not null)) {
                seated.Session.Tell(new ZONE_102_PROTOCOL.MSG_SIGILPARTNERLEFT());
            }
        }

        // todo: a player stepping off could leave the sigil unlit for others; is lighting it again for everyone enough?
        Timers.StartSingleTimer(RELIGHT_TIMER_KEY, new ZONE_102_PROTOCOL.MSG_SIGILRELIGHT(), s_relightDelay);
    }

    private ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY CreateEntry(int seat, ulong roomOwner, ulong partner) {
        var pad = Entity.ActiveGameObject;
        var arrival = SigilInfo.m_successLocations?.FirstOrDefault().ToString();

        return new ZONE_102_PROTOCOL.MSG_STARTSIGILENTRY {
            SigilLoc = GetPadLocation(),
            SigilGID = (ulong) pad.m_globalID,
            SigilType = SigilInfo.m_sigilType.ToString() ?? "",
            Radius = DEFAULT_INTERACTION_RADIUS,
            DestinationZone = DestinationZone,
            DestinationLoc = string.IsNullOrEmpty(arrival) ? DEFAULT_ARRIVAL_LOCATION : arrival,
            SlotIndex = seat,
            CountdownSeconds = SigilInfo.m_countdownTimer,
            SigilRef = Self,
            RoomOwnerCharId = roomOwner,
            PartnerCharId = partner,
        };
    }

    private int CountPlayerCircles() {
        return Math.Max(1, SigilFactory.GetPlayerCircles(SigilInfo?.m_sigilType.ToString() ?? "").Count);
    }

    private sealed record SigilSeat(ulong CharId, IActorRef Session);

}
