using System.Net.Sockets;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.DML;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Database;
using WizUnraveler.IO;

namespace Imlight.Server.Shared.Packets
{
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
            
            [DMLElement(DMLType.STR)] public string Name;
            [DMLElement(DMLType.USHRT)] public ushort Port;
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
    }
}
