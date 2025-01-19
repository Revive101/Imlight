/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
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

        // Search for the interaction object.
        var npc = GetZoneObject(message.GlobalID);
        if (npc == null) {
            Logger.Error("{0} searched for NPC by global ID {1} but one was not found",
                Logger.Args(wizard.CharId, message.GlobalID));

            return;
        }

        // Check if the interacted object contains a service memento component.
        var serviceMementoComponent = npc.GetComponentOfType<ServiceMementoComponent>();
        if (serviceMementoComponent == null) {
            Logger.Error("{0} interacted with NPC {1} but it does not contain a service memento component",
                Logger.Args(wizard.CharId, message.GlobalID));

            return;
        }

        var actor = SessionActor.ActorRef;
        var gameObj = GetActiveGameObject();
        var serviceName = message.ServiceName;
        var serviceIndex = message.ServiceIndex;
      
        // Inform the service memento component that the player is interacting with it.
        serviceMementoComponent.PlayerInteraction(actor, wizard, gameObj, serviceName, serviceIndex);
    }

    private void CloseShop(ulong charId) {
        var enableMovementStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = charId,
            State = StringHash.Compute("Moving"),
            IgnoreIfCurrentStateIsOff = 1
        };
        SendToSocket(enableMovementStateMsg);

        var clearWizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = charId,
            WizBangID = (uint) WizBangs.None
        };
        ZoneBroadcast(clearWizBangMsg, false);
    }
    
}
