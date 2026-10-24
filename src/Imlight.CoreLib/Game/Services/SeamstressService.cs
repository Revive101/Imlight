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
 * SEAMSTRESS SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Receives the stitch and unstitch messages of a player at a seamstress NPC and hands them to
 * SeamstressRequests.
 *
 * USAGE EXAMPLE:
 * The client sends MSG_STITCHITEMS or MSG_UNSTITCHITEMS after the seamstress window's confirm. Every
 * request is answered with one MSG_STITCHITEMSCONFIRM.
 *
 * NOTE:
 * A refusal shows the client only its generic failure box.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Seamstress;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class SeamstressService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new SeamstressService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_STITCHITEMS))]
    private void ReceiveStitchItems(WIZARD_12_PROTOCOL.MSG_STITCHITEMS message) {
        var wizard = GetActiveWizard();

        SendConfirm(wizard, CreateRequests(wizard).Stitch(message));
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_UNSTITCHITEMS))]
    private void ReceiveUnstitchItems(WIZARD_12_PROTOCOL.MSG_UNSTITCHITEMS message) {
        var wizard = GetActiveWizard();

        SendConfirm(wizard, CreateRequests(wizard).Unstitch(message));
    }

    private SeamstressRequests CreateRequests(Wizard wizard)
        => new(wizard, SessionActor.ActorRef, GetZoneObject, GetActiveGameObject, UnequipItem);

    private bool UnequipItem(ulong itemId)
        => AskOtherService<CHARACTER_103_PROTOCOL.MSG_UNEQUIPITEMRSP>(
            new CHARACTER_103_PROTOCOL.MSG_UNEQUIPITEM { ItemId = itemId }).Success;

    private void SendConfirm(Wizard wizard, bool success)
        => SendToSocket(new WIZARD_12_PROTOCOL.MSG_STITCHITEMSCONFIRM {
            Failure = success ? 0 : 1,
            WebFailure = 0,
            Credits = wizard.Account.Crowns
        });

}
