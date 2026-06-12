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
 * INTERACT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player interactions with game world objects, NPCs, and services, 
 * handling complex interaction routing mechanisms.
 * 
 * USAGE EXAMPLE:
 * Internal service processing player interaction messages within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal class InteractService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC))]
    private void ReceiveNpcInteract(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message) {
        var wizard = GetActiveWizard();

        // A player is closing their shop
        if (message.ServiceName == "") {
            CloseShop(wizard.CharId);
            return;
        }

        HandleInteraction(message, msg => msg.GlobalID, msg => msg.ServiceName, msg => msg.ServiceIndex);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_INTERACTOPTION))]
    private void ReceiveInteractOption(GAME_5_PROTOCOL.MSG_INTERACTOPTION message)
        => HandleInteraction(message, msg => msg.ObjectID, msg => "Interact", msg => (uint) msg.OptionIndex);

    private void HandleInteraction<T>(T message, Func<T, ulong> getObjectId, Func<T, string> getServiceName, Func<T, uint> getServiceIndex) {
        var wizard = GetActiveWizard();

        // Search for the interaction object.
        var npc = GetZoneObject(getObjectId(message));
        if (npc == null) {
            Logger.Error("{0} searched for NPC by global ID {1} but one was not found",
                Logger.Args(wizard.CharId, getObjectId(message)));

            return;
        }

        // Check if the interacted object contains a service memento component.
        var serviceMementoComponent = npc.GetComponentOfType<InteractServiceMementoComponent>();
        if (serviceMementoComponent == null) {
            Logger.Error("{0} interacted with NPC {1} but it does not contain a service memento component",
                Logger.Args(wizard.CharId, getObjectId(message)));

            return;
        }

        var actor = SessionActor.ActorRef;
        var gameObj = GetActiveGameObject();

        // Inform the service memento component that the player is interacting with it.
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEINTERACTION {
            GlobalID = getObjectId(message),
            ServiceName = getServiceName(message),
            ServiceIndex = getServiceIndex(message),
            PlayerActor = actor,
            PlayerCharacter = wizard,
            PlayerObject = gameObj
        };
        serviceMementoComponent.ActorRef.Tell(msg);
    }

    private void CloseShop(ulong charId) {
        var enableMovementStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = charId,
            State = StringHash.Compute("Moving"),
            IgnoreIfCurrentStateIsOff = 1
        };
        ZoneBroadcast(enableMovementStateMsg, false);

        var clearWizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = charId,
            WizBangID = (uint) WizBangs.None
        };
        ZoneBroadcast(clearWizBangMsg, false);
    }

}
