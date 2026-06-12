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