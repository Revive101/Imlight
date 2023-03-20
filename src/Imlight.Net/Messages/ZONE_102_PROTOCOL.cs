using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.DML;

namespace Imlight.Net.Messages
{
    public class ZONE_102_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 102;
        public string ProtocolType { get; } = "ZONE";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Zone General Messages.";
        
        public class MSG_ZONETRANSFERREQUEST : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 102;

            public SessionActor SessionActor;
            public string ZoneName;
            public IActorRef OldZone;
        }
        
        public class MSG_ZONETRANSFERREQUESTRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 102;

            public IActorRef NewZone;
            public ByteString CriticalObjects;
            public uint DynamicZoneId;
            public uint ErrorCode;
        }

        public class MSG_QUERYZONE : IServerMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 102;
        }

        public class MSG_QUERYZONERSP : IServerMessage
        {
            public byte MessageOrder { get; } = 4;
            public byte ServiceID { get; } = 102;

            public uint PlayerCount;
            public ByteString CriticalObjects;
            public uint DynamicZoneId;
        }
        
        public class MSG_ADDPLAYER : IServerMessage
        {
            public byte MessageOrder { get; } = 5;
            public byte ServiceID { get; } = 102;
            
            public IActorRef Player;
        }
        
        public class MSG_REMOVEPLAYER : IServerMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 102;
            
            public IActorRef Player;
        }

        public class MSG_ZONEBROADCAST : IServerMessage
        {
            public byte MessageOrder { get; } = 7;
            public byte ServiceID { get; } = 102;

            public IActorRef Sender;
            public INetworkMessage Message;
            public bool Selfless;
        }
        
        public class MSG_CREATENETWORKOBJECT : IServerMessage
        {
            public byte MessageOrder { get; } = 8;
            public byte ServiceID { get; } = 102;

            public IActorRef Sender;
            public TypeCache.CoreObject CoreObject;
            public bool Selfless;
        }

        public class MSG_CREATENETWORKOBJECTRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 9;
            public byte ServiceID { get; } = 102;

            public ulong GlobalID;
            public ulong PermID;
            public ushort MobileId;
        }

        public class MSG_QUERYZONEOBJECTS : IServerMessage
        {
            public byte MessageOrder { get; } = 10;
            public byte ServiceID { get; } = 102;
        }
        
        public class MSG_QUERYZONEOBJECTSRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 11;
            public byte ServiceID { get; } = 102;

            public List<TypeCache.CoreObject> CoreObjects;
        }

        public class MSG_QUERYLOCALGAMEOBJECT : IServerMessage
        {
            public byte MessageOrder { get; } = 12;
            public byte ServiceID { get; } = 102;
        }

        public class MSG_QUERYLOCALGAMEOBJECTRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 13;
            public byte ServiceID { get; } = 102;

            public ulong GlobalID;
            public ulong PermID;
            public ushort MobileId;
        }
    }
}