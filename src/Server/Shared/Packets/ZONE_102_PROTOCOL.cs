/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.Cache;
using WizUnraveler.DML;
using Imlight.Server.Shared.Networking;

namespace Imlight.Server.Shared.Packets
{
    public class ZONE_102_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 102;
        public string ProtocolType { get; } = "ZONE";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Zone General Messages.";
        
        public class MSG_QUERYZONE : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 102;
            
            public string ZoneName;
        }
        
        public class MSG_QUERYZONERSP : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 102;

            public IActorRef ZoneActorRef;
            public TypeCache.CoreObject[] ZoneObjects;
            public uint DynamicZoneId;
            public uint ErrorCode;
        }

        public class MSG_ADDPLAYER : IServerMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 102;
            
            public IActorRef Player;
            public TypeCache.CoreObject PlayerObject;
        }
        
        public class MSG_ADDPLAYERRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 4;
            public byte ServiceID { get; } = 102;

            public TypeCache.CoreObject PlayerObject;
        }
        
        public class MSG_REMOVEPLAYER : IServerMessage
        {
            public byte MessageOrder { get; } = 5;
            public byte ServiceID { get; } = 102;
            
            public IActorRef Player;
            public ulong GlobalId;
            public bool IsZoneTransfer;
        }

        public class MSG_ZONEBROADCAST : IServerMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 102;

            public IActorRef Sender;
            public INetworkMessage Message;
            public bool Selfless;
        }
    }
}