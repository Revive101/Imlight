/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

internal sealed class PROCESS_107_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 107;
    public string ProtocolType { get; } = "PROCESS";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Process Messages.";

    internal sealed class MSG_NEW_MINIGAME_PROCESS : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 107;

        public byte MinigameIndex;
        public string MinigameName;
        public IActorRef Owner;

    }

    internal sealed class MSG_PROCESS_DETAILS : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 107;

        public string ProcessName;
        public IActorRef ProcessActorRef;
        public uint ProcessId;

    }

    internal sealed class MSG_PROCESS_ACTIVITY_CHECK : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 107;

    }

    internal sealed class MSG_PROCESS_KILLED : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 107;

        public uint ProcessId;

    }

}