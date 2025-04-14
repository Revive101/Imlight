/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * DYNAMOD SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages dynamic character state modifications and state transitions 
 * within the game server session.
 * 
 * USAGE EXAMPLE:
 * Internal service handling character state changes and dynamic 
 * modification management.
 * 
 * NOTE:
 * - Manages addition and removal of dynamic modifications
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

public class DynaModService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new DynaModService(parentActor));

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ENTERSTATE))]
    private void ReceiveEnterState(CHARACTER_103_PROTOCOL.MSG_ENTERSTATE message) {
        var activeWizard = GetActiveWizard();
        var activeWizardGameObject = GetActiveGameObject();

        var objState = activeWizard.EnterState(message.StateName);
        if (objState is null) {
            Logger.Error("Failed to enter state {0} for wizard {1}", Logger.Args(message.StateName, activeWizard.CharId));

            return;
        }

        // Echo the state change to the client.
        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = activeWizardGameObject.m_globalID,
            State = StringHash.Compute(message.StateName)
        };

        var isPublicStateChange = !objState.m_privateState;
        if (isPublicStateChange) {
            ZoneBroadcast(stateMsg, false);
        }
        else {
            SendToSocket(stateMsg);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD))]
    private void ReceiveAddDynaMod(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD message) {
        var wizard = GetActiveWizard();
        var zoneName = message.DynaMod.m_zoneName;
        var dynaModClientTag = message.DynaMod.m_dynaModClientTag;
        var dynaModState = message.DynaMod.m_dynaModState;

        wizard.AddDynamod(zoneName, dynaModClientTag, dynaModState);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD))]
    private void ReceiveRemoveDynaMod(CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD message) {
        var wizard = GetActiveWizard();
        var dynaModClientTag = message.DynaMod.m_dynaModClientTag;

        wizard.RemoveDynamod(dynaModClientTag);
    }

}
