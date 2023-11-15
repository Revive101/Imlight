/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Net.Sockets;
using Akka.Actor;
using Imlight.Common.IO;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Game.Models;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class SERVER_100_PROTOCOL : IServerProtocol
{
    public byte ServiceID { get; } = 100;
    public string ProtocolType { get; } = "SERVER";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Server General Messages.";

    public class MSG_CREATEGAMESERVER : IServerMessage
    {
        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 100;

        public string Name;
        public ushort Port;
    }

    public class MSG_ALLOCATESOCKET : IServerMessage
    {
        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 100;

        public Socket Socket;
    }

    public class MSG_DEALLOCATESOCKET : IServerMessage
    {
        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 100;

        public ushort Id;
        public Socket Socket;
        public string Ip;
    }

    public class MSG_QUERYACTORFACTORY : IServerMessage
    {
        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 100;
    }

    public class MSG_ACTORFACTORYINFO : IServerMessage
    {
        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 100;

        public IActorRef Reference;
    }

    public class MSG_QUERYSERVER : IServerMessage
    {
        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 100;

        public bool IsLocal;
    }

    public class MSG_QUERYGAMESERVERS : IServerMessage
    {
        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 100;

        public bool IsLocal;
    }

    public class MSG_PLAYERENQUEUED : IServerMessage
    {
        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 100;

        public SessionActor SessionActor;
        public ByteString Key;
        public bool VIPEntry;
    }

    public class MSG_SERVERINFO : IServerMessage
    {
        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 100;

        public ByteString IP;
        public int Port;
        public ushort PlayerCount;
        public TcpListener TcpClient;
        public IActorRef ActorRef;
    }

    public class MSG_CREATEKEY : IServerMessage
    {
        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 100;

        public Account Account;
    }

    public class MSG_CREATEKEYRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 100;

        public ByteString Key;
    }

    public class MSG_VALIDATESESSIONKEY : IServerMessage
    {
        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 100;

        public ByteString Key;
        public ulong UserID;
        public SessionActor SessionActor;
    }

    public class MSG_VALIDATESESSIONKEYRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 100;

        // 0: Success
        // 1: Failed
        // @todo: make these string IDs instead.
        public int ErrorCode;
        public Account Account;
    }

    public class MSG_PING : IServerMessage
    {
        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 100;

        public long Ping;
    }

    public class MSG_INITIALIZE : IServerMessage
    {
        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 100;
    }

    public class MSG_INITIALIZE_COMPLETE : IServerMessage
    {
        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 100;
    }

    public class MSG_COMMAND : IServerMessage
    {
        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 100;

        public WideByteString CommandText;
        public IActorRef ActorRef;
        public CoreObject CoreObject;
        public Character PlayerCharacter;
        public Account Account;
    }

    public class MSG_COMMANDRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 100;

        public WideByteString CommandText;
        public bool Failed;
        public ByteString ResponseText;
    }

    public class MSG_PLAYERENQUEUEDRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 100;

        public int PositionInQueue;
        public int Status;
        public bool Failed;
    }
}
