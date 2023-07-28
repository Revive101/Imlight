/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.Cache;
using WizUnraveler.DML;
using Imlight.Server.Shared.Networking;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using WizUnraveler.Secrets;

namespace Imlight.Server.Shared.Packets
{
    public class ZONE_102_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 102;
        public string ProtocolType { get; } = "ZONE";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Zone General Messages.";
        
        public class MSG_ZONETRANSFER : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 102;
            
            public string ZoneName;
            public string Location;
            public bool SendToClient = true;
        }
        
        public class MSG_ZONETRANSFERRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 102;

            public IActorRef ZoneActorRef;  
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
        
        public class MSG_REMOVEPLAYERRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 5;
            public byte ServiceID { get; } = 102;
        }

        public class MSG_ZONEBROADCAST : IServerMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 102;

            public IActorRef Sender;
            public INetworkMessage Message;
            public bool Selfless;
        }
        
        public class MSG_ADDOBJECT : IServerMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 102;

            public TypeCache.CoreObject CoreObject;
        }
        
        public class MSG_ADDOBJECTRSP : IServerMessage
        {
            public byte MessageOrder { get; } = 7;
            public byte ServiceID { get; } = 102;

            public IActorRef ActorRef;
            public ushort MobileId;
        }
        
        public class MSG_ADDPATH : IServerMessage
        {
            public byte MessageOrder { get; } = 8;
            public byte ServiceID { get; } = 102;
            
            public GID Id;
            public ByteString Name;
            public List<TypeCache.NodeObject> Nodes;
            public List<TypeCache.SpawnObject> Creatures;
        }
        
        public class MSG_ADDCREATURE : IServerMessage
        {
            public byte MessageOrder { get; } = 9;
            public byte ServiceID { get; } = 102;

            public IActorRef ObjectIdentity;
            public TypeCache.CoreObject CoreObject;
        }

        public class MSG_ADDVOLUME : IServerMessage
        {
            public byte MessageOrder { get; } = 11;
            public byte ServiceID { get; } = 102;

            public TypeCache.CoreObject CoreObject;
            public ServerTypeCache.Volume Volume;
        }
        
        public class MSG_ADDTRIGGER : IServerMessage
        {
            public byte MessageOrder { get; } = 12;
            public byte ServiceID { get; } = 102;

            public ServerTypeCache.Trigger Trigger;
        }
        
        public class MSG_TRIGGER : IServerMessage
        {
            public byte MessageOrder { get; } = 13;
            public byte ServiceID { get; } = 102;

            public ByteString TriggerName;
            public IActorRef Suspect;
        }
        
        public class MSG_TRIGGERQUERY : IServerMessage
        {
            public byte MessageOrder { get; } = 14;
            public byte ServiceID { get; } = 102;

            public TypeCache.CoreObject CoreObject;
            public IActorRef Suspect;
        }

        public class MSG_ZONEOBJECTBROADCAST : IServerMessage
        {
            public byte MessageOrder { get; } = 14;
            public byte ServiceID { get; } = 102;

            public IActorRef Source;
            public IServerMessage[] Messages;
        }
    }
}