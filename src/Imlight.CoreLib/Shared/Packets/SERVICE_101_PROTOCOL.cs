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

using System.Collections.Generic;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

internal sealed class SERVICE_101_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 104;
    public string ProtocolType { get; } = "ACCOUNT";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Server Account Messages.";

    public class MSG_QUERYMESSAGESERVICEIDENTITY : IServerMessage {
        
        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 101;
        
    }

    public class MSG_MESSAGESERVICEIDENTITY : IServerMessage {
        
        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 101;

        public MessageService Service;
        
    }

    public class MSG_QUERYUNLOADEDSERVICES : IServerMessage {
        
        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 101;
        
    }

    public class MSG_QUERYLOADEDSERVICES : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_SERVICESLIST : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 101;

        public List<System.Type> Services;

    }

    public class MSG_OPCODE_HALT : IServerMessage {

        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_OPCODE_RESUME : IServerMessage {

        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_DISPOSE : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_PREDISPOSE : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_GETALLSERVICES : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 101;

    }

    public class MSG_ATTACHCOMPLETE : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 101;

    }

    /// <summary>
    /// Timer callback carrying deferred teleport parameters through a 2s delay.
    /// </summary>
    public class MSG_TELEPORT_DELAY : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 101;

        public string DestinationZone;
        public string DestinationLocation;
        public bool MakePrivate;
        public ulong OwnerCharId;

    }

    /// <summary>
    /// Timer signal indicating the 2s recall delay has elapsed.
    /// </summary>
    public class MSG_RECALL_DELAY : IServerMessage {

        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 101;

    }

    /// <summary>
    /// Timer signal indicating the zone-transfer cleanup delay has elapsed.
    /// </summary>
    public class MSG_ZONETRANSFER_DELAY : IServerMessage {

        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 101;

    }

    /// <summary>
    /// Timer signal indicating the attach timeout has elapsed — the client
    /// never sent MSG_ATTACH after opening a new connection.
    /// </summary>
    public class MSG_ATTACH_TIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 101;

    }

    /// <summary>
    /// Registers fallback zone data on the GameServer, keyed by the client's
    /// remote IP, so the new session can recover if MSG_ATTACH never arrives.
    /// </summary>
    public class MSG_REGISTER_FALLBACK : IServerMessage {

        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 101;

        public string RemoteIp;
        public ulong UserId;
        public ulong CharId;
        public string FallbackZone;
        public uint FallbackZoneId;
        public string FallbackLocation;
        public string GameServerIp;
        public ushort GameServerPort;

    }

    /// <summary>
    /// Queries the GameServer for fallback zone data by remote IP.
    /// </summary>
    public class MSG_QUERY_FALLBACK : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 101;

        public string RemoteIp;

    }

    /// <summary>
    /// Response to <see cref="MSG_QUERY_FALLBACK"/> with fallback zone data
    /// (if found).
    /// </summary>
    public class MSG_QUERY_FALLBACK_RSP : IServerMessage {

        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 101;

        public bool Found;
        public ulong UserId;
        public ulong CharId;
        public string FallbackZone;
        public uint FallbackZoneId;
        public string FallbackLocation;
        public string GameServerIp;
        public ushort GameServerPort;

    }

    /// <summary>
    /// Removes a fallback registration from the GameServer (called on
    /// successful attach to clean up).
    /// </summary>
    public class MSG_REMOVE_FALLBACK : IServerMessage {

        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 101;

        public string RemoteIp;

    }

}
