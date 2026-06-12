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

internal class DynaModService(SessionActor sessionActor) : MessageService(sessionActor) {

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

        if (message.DynaMod is null) {
            Logger.Error("Received MSG_ADDDYNAMOD with null DynaMod data for wizard {0}",
                Logger.Args(wizard.CharId));
                
            return;
        }

        var zoneName = message.DynaMod.m_zoneName;
        var dynaModClientTag = message.DynaMod.m_dynaModClientTag;
        var dynaModState = message.DynaMod.m_dynaModState;

        wizard.AddDynamod(zoneName, dynaModClientTag, dynaModState);

        // Broadcast the state change to the zone.
        // Objects that match the client tag will apply the state change.
        var stateChangeMsg = new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
            ObjectName = dynaModClientTag,
            StateName = dynaModState,
            ExclusiveToSender = true,
            Sender = SessionActor.ActorRef
        };
        ZoneBroadcastNoPlayers(stateChangeMsg);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD))]
    private void ReceiveRemoveDynaMod(CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD message) {
        var wizard = GetActiveWizard();
        var dynaModClientTag = message.DynaMod.m_dynaModClientTag;

        wizard.RemoveDynamod(dynaModClientTag);
    }

}
