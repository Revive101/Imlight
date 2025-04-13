/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
using Imlight.CoreLib.WizardData.Models.World;

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
