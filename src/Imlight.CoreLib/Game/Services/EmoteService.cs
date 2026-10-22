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
 * EMOTE SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player emote interactions and synchronizes emote animations and
 * text messages to other players within the active zone.
 * 
 * USAGE EXAMPLE:
 * Internal service attached to SessionActor for handling emote-related
 * network messages.
 * 
 * NOTE:
 * - Originator players predictively play their emote locally (ExcludeOriginator = 1).
 * - Broadcasts MSG_PIIRADIALMENUPLAYEMOTE (Service 56, Order 73) selflessly
 *   to other players in the zone.
 * 
 * Created by: Revive101
 * Version: KALI 1.0
 * Last Updated: 10/19/2026
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using System;

namespace Imlight.CoreLib.Game.Services;

internal class EmoteService(SessionActor sessionActor) : MessageService(sessionActor) {

    private static readonly uint ResetStateId = StringHash.Compute("Unremarkable");
    private static readonly uint EmoteStateId = StringHash.Compute("ActionEmoting");

    private DateTime _lastEmoteTime = DateTime.MinValue;
    private string _lastEmoteAnimation = string.Empty;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new EmoteService(parentActor));

    /// <summary>
    /// Handles radial menu emote play requests from the client.
    /// Broadcasts the emote animation and speech text to other players in the zone.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE))]
    private void ReceiveRequestPIIRadialMenuPlayEmote(WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE message) {
        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.EmoteAnimationName;

        var wizard = GetActiveWizard();
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.EmoteAnimationName,
            EmoteText = message.EmoteText
        };

        ZoneBroadcast(broadcastMsg);
        BroadcastEmoteAnimation(wizard.GameObjectID, message.EmoteAnimationName, true);
    }

    /// <summary>
    /// Handles core radial menu emote requests. If not already broadcast via
    /// <see cref="WIZARD3_56_PROTOCOL.MSG_REQUESTPIIRADIALMENUPLAYEMOTE"/>, synchronizes to zone.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_CORE_PIIRADIALMENUEMOTE))]
    private void ReceiveCorePIIRadialMenuEmote(WIZARD3_56_PROTOCOL.MSG_CORE_PIIRADIALMENUEMOTE message) {
        // Prevent duplicate broadcast if MSG_REQUESTPIIRADIALMENUPLAYEMOTE was just handled.
        if (string.Equals(_lastEmoteAnimation, message.EmoteAnimationName, StringComparison.Ordinal)
&& (DateTime.UtcNow - _lastEmoteTime) < TimeSpan.FromSeconds(1)) {
            return;
        }


        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.EmoteAnimationName;

        var wizard = GetActiveWizard();
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.EmoteAnimationName,
            EmoteText = string.Empty
        };

        ZoneBroadcast(broadcastMsg, isSelfless: true, targets: ZoneBroadcastTarget.Players);
        BroadcastEmoteAnimation(wizard.GameObjectID, message.EmoteAnimationName, isSelfless: message.ExcludeOriginator == 1);
    }

    /// <summary>
    /// Handles classic core emote messages from GAME_5_PROTOCOL.
    /// </summary>
    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CORE_EMOTE))]
    private void ReceiveCoreEmote(GAME_5_PROTOCOL.MSG_CORE_EMOTE message) {

        _lastEmoteTime = DateTime.UtcNow;
        _lastEmoteAnimation = message.Name;

        var wizard = GetActiveWizard();
        var broadcastMsg = new WIZARD3_56_PROTOCOL.MSG_PIIRADIALMENUPLAYEMOTE {
            SourceID = wizard.GameObjectID,
            EmoteAnimationName = message.Name,
            EmoteText = string.Empty
        };

        ZoneBroadcast(broadcastMsg);
        BroadcastEmoteAnimation(wizard.GameObjectID, message.Name, message.ExcludeOriginator == 1);
    }

    /// <summary>
    /// Broadcasts MSG_ENTERSTATE with EmoteStateOverrideInfo.
    /// Resets the state machine via "Unremarkable" first so consecutive emotes always trigger.
    /// </summary>
    private void BroadcastEmoteAnimation(ulong gameObjectId, string emoteAnimationName, bool isSelfless) {
        if (string.IsNullOrWhiteSpace(emoteAnimationName)) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard.CurrentAnimationState == EmoteStateId) {
            var resetMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                GameObjectID = gameObjectId,
                State = ResetStateId,
                Data = new ByteString(),
                IgnoreIfCurrentStateIsOff = 0,
            };
            ZoneBroadcast(resetMsg, isSelfless: isSelfless);
        }

        var stateEmote = new EmoteStateOverrideInfo {
            m_stateNameID = EmoteStateId,
            m_emoteName = emoteAnimationName,
            m_particleAsset = string.Empty,
            m_loop = false,
            m_particleNode = string.Empty,
            m_soundAsset = string.Empty,
            m_wizBangID = 0
        };

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );

        if (!serializer.Serialize(stateEmote, 1, out var emoteData)) {
            Logger.Error("Failed to serialize EmoteStateOverrideInfo for {Anim}",
                Logger.Args(emoteAnimationName));

            return;
        }

        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = gameObjectId,
            State = EmoteStateId,
            Data = emoteData,
            IgnoreIfCurrentStateIsOff = 0,
        }, isSelfless);

        wizard.CurrentAnimationState = EmoteStateId;
    }

    /// <summary>
    /// Handles radial menu layout updates from client. The client sends this message, when the radial menu is closed.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU))]
    private void ReceivePIIUpdateEmotesRadialMenu(WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU message) {
        var wizard = GetActiveWizard();
        // Normalize empty or null blobs
        var menu1 = message.RadialMenuData.IsEmpty ? RadialMenuData.Empty : message.RadialMenuData;
        var menu2 = message.RadialMenu2Data.IsEmpty ? RadialMenuData.Empty : message.RadialMenu2Data;
        var menu3 = message.RadialMenu3Data.IsEmpty ? RadialMenuData.Empty : message.RadialMenu3Data;

        // Validate structure before saving to DB
        if (!RadialMenuData.TryFromByteString(menu1, out _) ||
            !RadialMenuData.TryFromByteString(menu2, out _) ||
            !RadialMenuData.TryFromByteString(menu3, out _)) {
            Logger.Warning("[EmoteService] Received invalid radial menu data from character {Id}",
                Logger.Args(wizard.CharId));
            return;
        }
        // Skip DB write if nothing changed
        if (wizard.RadialMenuData == menu1 &&
            wizard.RadialMenu2Data == menu2 &&
            wizard.RadialMenu3Data == menu3) {
            return;
        }

        wizard.RadialMenuData = menu1;
        wizard.RadialMenu2Data = menu2;
        wizard.RadialMenu3Data = menu3;
        WizardCollection.UpdateCharacterRadialMenuData(wizard);
        Logger.Information("[EmoteService] Saved updated radial menu data for character {Id}",
            Logger.Args(wizard.CharId));
    }

    /// <summary>
    /// Handles client request to convert emotes to the PII system.
    /// </summary>
    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTONDEMANDCONVERTEMOTESTOPIISYSTEM))]
    private void ReceiveRequestOnDemandConvertEmotes(WIZARD3_56_PROTOCOL.MSG_REQUESTONDEMANDCONVERTEMOTESTOPIISYSTEM message) {
        // Client requesting on-demand conversion.
    }

    /// <summary>
    ///  Loads the radial menu from DB
    /// </summary>
    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        var updateMsg = new WIZARD3_56_PROTOCOL.MSG_PIIUPDATEEMOTESRADIALMENU {
            RadialMenuData = wizard.RadialMenuData.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenuData,
            RadialMenu2Data = wizard.RadialMenu2Data.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenu2Data,
            RadialMenu3Data = wizard.RadialMenu3Data.IsEmpty ? RadialMenuData.Empty : wizard.RadialMenu3Data
        };
        SendToSocket(updateMsg);

        Logger.Information("[EmoteService] Restored radial menu data to client for character {Id}",
            Logger.Args(wizard.CharId));
    }
}
