/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class SERVICE_101_PROTOCOL : IServerProtocol {
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
}
