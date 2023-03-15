using System.Collections.Generic;
using System.Net.Sockets;
using Akka.Actor;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.DML;

namespace Imlight.Net.Messages
{
    public sealed class SERVER_100_PROTOCOL : INetworkProtocol
    {
        public byte ServiceID { get; } = 100;
        public string ProtocolType { get; } = "SERVER";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Server General Messages.";
        
        public INetworkMessage Dispatch(byte msgid)
        {
            switch (msgid)
            {
                case (1): return new MSG_CREATEGAMESERVER();
                case (2): return new MSG_ALLOCATESOCKET();
                case (3): return new MSG_DEALLOCATESOCKET();
                case (4): return new MSG_QUERYACTORFACTORY();
                case (5): return new MSG_ACTORFACTORYINFO();
                case (9): return new MSG_SERVERINFO();
                case (10): return new MSG_CREATEKEY();
                case (11): return new MSG_CREATEKEYRSP();
                case (12): return new MSG_VALIDATESESSIONKEY();
                case (13): return new MSG_VALIDATESESSIONKEYRSP();
                default: return null;
            }
        }
        
        public class MSG_CREATEGAMESERVER : INetworkMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 100;
            
            [DMLElement(DMLType.STR)] public string Name;
            [DMLElement(DMLType.USHRT)] public ushort Port;
        }

        public class MSG_ALLOCATESOCKET : INetworkMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 100;

            public Socket Socket;
        }
        
        public class MSG_DEALLOCATESOCKET : INetworkMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 100;

            public ushort ID;
        }

        public class MSG_QUERYACTORFACTORY : INetworkMessage
        {
            public byte MessageOrder { get; } = 4;
            public byte ServiceID { get; } = 100;
        }
        
        public class MSG_ACTORFACTORYINFO : INetworkMessage
        {
            public byte MessageOrder { get; } = 5;
            public byte ServiceID { get; } = 100;

            public IActorRef Reference;
        }

        public class MSG_QUERYSERVER : INetworkMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 100;
        }

        public class MSG_QUERYGAMESERVERS : INetworkMessage
        {
            public byte MessageOrder { get; } = 7;
            public byte ServiceID { get; } = 100;
        }
        
        public class MSG_PLAYERENQUEUED : INetworkMessage
        {
            public byte MessageOrder { get; } = 8;
            public byte ServiceID { get; } = 100;

            public SessionActor SessionActor;
            public bool VIPEntry;
        }

        public class MSG_SERVERINFO : INetworkMessage
        {
            public byte MessageOrder { get; } = 9;
            public byte ServiceID { get; } = 100;

            public ByteString IP;
            public int Port;
            public ushort PlayerCount;
            public TcpListener TcpClient;
            public IActorRef ActorRef;
        }

        public class MSG_CREATEKEY : INetworkMessage
        {
            public byte MessageOrder { get; } = 10;
            public byte ServiceID { get; } = 100;
            
            public Account Account;
        }
        
        public class MSG_CREATEKEYRSP : INetworkMessage
        {
            public byte MessageOrder { get; } = 11;
            public byte ServiceID { get; } = 100;

            public ByteString Key;
        }

        public class MSG_VALIDATESESSIONKEY : INetworkMessage
        {
            public byte MessageOrder { get; } = 12;
            public byte ServiceID { get; } = 100;

            public ByteString Key;
            public ulong UserID;
        }
        
        public class MSG_VALIDATESESSIONKEYRSP : INetworkMessage
        {
            public byte MessageOrder { get; } = 13;
            public byte ServiceID { get; } = 100;

            // 0: Success
            // 1: Failed
            // @todo: make these string IDs instead.
            public int ErrorCode;
            public Account Account;
        }
    }
}