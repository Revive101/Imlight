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
 * PRIVACY OPTIONS SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Answers the settings window's privacy options request and saves the options the player changes.
 * 
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory.
 * 
 * NOTE:
 * The client expects no reply to an update. Every value is stored as 0 or 1.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class PrivacyOptionsService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PrivacyOptionsService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REQUESTPRIVACYOPTIONS))]
    private void ReceiveRequestPrivacyOptions() {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var options = wizard.PrivacyOptions;

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_RESPONSEPRIVACYOPTIONS {
            AllowFriendRequest = options.AllowFriendRequest,
            AllowFriendTeleport = options.AllowFriendTeleport,
            AllowTradeRequest = options.AllowTradeRequest,
            AllowHatchRequest = options.AllowHatchRequest,
            AllowPartyInvites = options.AllowPartyInvites,
            HidePVPEnemyChat = options.HidePVPEnemyChat,
            LimitHomeToFriends = options.LimitHomeToFriends,
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_UPDATEPRIVACYOPTIONS))]
    private void ReceiveUpdatePrivacyOptions(WIZARD_12_PROTOCOL.MSG_UPDATEPRIVACYOPTIONS message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var options = wizard.PrivacyOptions;

        options.AllowFriendRequest = AsFlag(message.AllowFriendRequest);
        options.AllowFriendTeleport = AsFlag(message.AllowFriendTeleport);
        options.AllowTradeRequest = AsFlag(message.AllowTradeRequest);
        options.AllowHatchRequest = AsFlag(message.AllowHatchRequest);
        options.AllowPartyInvites = AsFlag(message.AllowPartyInvites);
        options.HidePVPEnemyChat = AsFlag(message.HidePVPEnemyChat);
        options.LimitHomeToFriends = AsFlag(message.LimitHomeToFriends);

        WizardCollection.UpdateCharacterPrivacyOptions(wizard);
    }

    private static byte AsFlag(byte value)
        => (byte) (value == 0 ? 0 : 1);

}
