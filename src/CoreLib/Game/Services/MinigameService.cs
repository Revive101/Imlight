/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Services;

internal class MinigameService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const ushort MAGIC_HEADER = (ushort) 0xF00Du;

    private IActorRef _minigameJob;
    private uint _minigameProcessId;

    // Akka.net props
    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new MinigameService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete() {
        var wizard = GetActiveWizard();
        var wizardZone = wizard.Zone;

        // If the player's zone is a minigame zone, start the appropriate minigame script.
        if (!MinigameConfig.IsMinigameZone(wizardZone)) {
            return;
        }

        // Minigame script format looks like 'SkullRiders/Client.lua'
        var minigameScript = MinigameConfig.GetMinigameScript(wizardZone);

        // Ask the server for a new minigame process.
        var msg = new PROCESS_107_PROTOCOL.MSG_NEW_MINIGAME_PROCESS {
            MinigameName = minigameScript.Split('/')[0],
            Owner = SessionActor.ActorRef
        };
        var reply = AskServer<PROCESS_107_PROTOCOL.MSG_PROCESS_DETAILS>(msg);
        _minigameJob = reply.ProcessActorRef;
        _minigameProcessId = reply.ProcessId;

        var minigameStartProcess = new GAME_5_PROTOCOL.MSG_START_CLIENT_PROCESS {
            OwnerGID = wizard.CharId,
            JobID = reply.ProcessId,
            ScriptName = minigameScript
        };
        SendToSocket(minigameStartProcess);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_MINIGAMESELECT))]
    private void ReceiveMinigameSelect(WIZARD_12_PROTOCOL.MSG_MINIGAMESELECT message) {
        var minigameIndex = message.Index;
        var minigameInfo = MinigameConfig.GetMinigameInfo(minigameIndex);
        var wizardName = GetActiveWizard().PlayerNameBehavior.GetWizardName();

        if (minigameInfo == null) {
            Logger.Error("{0} tried to start up minigame at non-existing index {1}", 
                Logger.Args(wizardName, minigameIndex));

            return;
        }

        var msg = new WIZARD_12_PROTOCOL.MSG_ENTERMINIGAME();
        SendToSocket(msg);

        Logger.Debug("{0} started minigame {1} at index {2}", 
            Logger.Args(wizardName, minigameInfo.m_name, minigameIndex));

        DoZoneTransfer(minigameInfo.m_zone, true, "Start");
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_MESSAGE_PROCESS))]
    private void ReceiveMessageProcess(GAME_5_PROTOCOL.MSG_MESSAGE_PROCESS message) {
        // The raw message is just like any other message sent by the client.
        // However, it does not include the magic header or body.
        // It immediately begins with the DML header, detailing the service ID, message ID, length, and finally the payload.
        // Find more @ https://revive101.github.io/Imlight-docs/internals/systems/dml/serialization.html#dml-header.

        // Our message serializer doesn't quite support that yet, so we'll be using a shortcut:
        // Add the magic header and body manually, then send it to the message serializer.
        var messageRaw = message.Message;
        var writer = new BitWriter();

        // Write the magic header and the length of the message.
        writer.WriteUInt16(MAGIC_HEADER);
        writer.WriteUInt16((ushort) messageRaw.Length);

        // Write the body.
        writer.WriteUInt8(0);  // IsControl
        writer.WriteUInt8(0);  // OpCode
        writer.WriteUInt16(0); // Padding

        // Write payload.
        writer.WriteBytes(messageRaw);

        // Deserialize using the MessageSerializer.
        var deserializedMsg = MessageSerializer.Decode(writer.GetData());

        if (deserializedMsg == null) {
            Logger.Error("Failed to deserialize message process {0}", 
                Logger.Args(messageRaw));

            return;
        }

        foreach (var msg in deserializedMsg) {
            // Forward the message to the minigame job.
            _minigameJob.Tell(msg, SessionActor.ActorRef);
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_PROCESS_TERMINATED))]
    private void ReceiveClientKilledProcess(GAME_5_PROTOCOL.MSG_CLIENT_PROCESS_TERMINATED message) {
        // The client has terminated the minigame process.
        if (message.JobID == _minigameProcessId) {
            _minigameJob.Tell(message, SessionActor.ActorRef);
        }
    }

    [MessageHandler(typeof(PROCESS_107_PROTOCOL.MSG_PROCESS_KILLED))]
    private void ReceiveProcessKilled(PROCESS_107_PROTOCOL.MSG_PROCESS_KILLED message) {
        if (message.ProcessId == _minigameProcessId) {
            // Our minigame job has been killed.
            // Inform the client the process has been terminated.
            var terminateMsg = new GAME_5_PROTOCOL.MSG_KILL_CLIENT_PROCESS { JobID = _minigameProcessId };
            SendToSocket(terminateMsg);

            var leaveMinigameMsg = new WIZARD_12_PROTOCOL.MSG_LEAVEMINIGAME();
            SendToSocket(leaveMinigameMsg);

            var wizard = GetActiveWizard();
            DoZoneTransfer(wizard.PreviousZone, false, "Start");
        }
    }

}