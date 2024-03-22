/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;

namespace Imlight.CoreLib.Shared.Packets;

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

		public string DestinationZone;
		public string DestinationLocation;
		public bool SendToClient = true;
	}

	public class MSG_ZONETRANSFERRSP : IServerMessage
	{
		public byte MessageOrder { get; } = 2;
		public byte ServiceID { get; } = 102;

		public IActorRef ZoneActorRef;
		public ushort MobileId;
		public uint DynamicZoneId;
        public string ZoneDisplayName;
        public Vector3 Location;
        public float Orientation;
		public uint ErrorCode;
	}

	public class MSG_ADDPLAYER : IServerMessage
	{
		public byte MessageOrder { get; } = 3;
		public byte ServiceID { get; } = 102;

		public IActorRef Player;
		public TypeCache.CoreObject PlayerObject;
        public Wizard Wizard;
        public string ActualWizardName;
	}

	public class MSG_ADDPLAYERRSP : IServerMessage
	{
		public byte MessageOrder { get; } = 4;
		public byte ServiceID { get; } = 102;

		public TypeCache.CoreObject WizardGameObject;
	}

	public class MSG_REMOVEPLAYER : IServerMessage
	{
		public byte MessageOrder { get; } = 5;
		public byte ServiceID { get; } = 102;

		public IActorRef Player;
		public ulong GlobalId;
		public bool IsPlayerStillConnected;
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
		public IMessage Message;
		public bool Selfless;
	}

	public class MSG_ADDOBJECT : IServerMessage
	{
		public byte MessageOrder { get; } = 6;
		public byte ServiceID { get; } = 102;

		public TypeCache.CoreObject CoreObject;
		public TypeCache.CoreTemplate Template;
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

	public class MSG_FISHINTERACTION : IServerMessage
	{
		public byte MessageOrder { get; } = 14;
		public byte ServiceID { get; } = 102;

		public TypeCache.CoreObject CoreObject;
		public IActorRef Suspect;
        public bool IsCreature;
	}

	public class MSG_ZONEOBJECTBROADCAST : IServerMessage
	{
		public byte MessageOrder { get; } = 14;
		public byte ServiceID { get; } = 102;

		public IActorRef Source;
		public IServerMessage[] Messages;
	}

	public class MSG_ADDCOMBATSIGIL : IServerMessage
	{
		public byte MessageOrder { get; } = 15;
		public byte ServiceID { get; } = 102;

		public TypeCache.CoreObject CoreObject;
		public TypeCache.CoreTemplate Template;
	}

	public class MSG_ADDCOMBATSIGILRSP : IServerMessage
	{
		public byte MessageOrder { get; } = 16;
		public byte ServiceID { get; } = 102;

		public IActorRef ActorRef;
		public ushort MobileId;
	}

	public class MSG_REQUESTCOMBATSIGIL : IServerMessage
	{
		public byte MessageOrder { get; } = 17;
		public byte ServiceID { get; } = 102;

		public Dictionary<IActorRef, TypeCache.CoreObject> StartingParticipants;
	}

    public class MSG_QUERYZONEOBJECT : IServerMessage
    {
        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 102;

        public ulong GlobalID;
    }

    public class MSG_QUERYZONEOBJECTRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 102;

        public WizardZoneObject ZoneObject;
    }

    public class MSG_OBJECTSTATUSCHECK : IServerMessage
    {
        public byte MessageOrder { get; } = 20;
        public byte ServiceID { get; } = 102;
    }

    public class MSG_OBJECTSTATUSCHECKRSP : IServerMessage
    {
        public byte MessageOrder { get; } = 21;
        public byte ServiceID { get; } = 102;

        public WizardZoneObject ZoneObject;
        public TypeCache.CoreObject CoreObject;
        public bool Failure;
        public string Error;
    }

    public class MSG_PLAYERMOVEINTERVAL : IServerMessage
    {
        public byte MessageOrder { get; } = 22;
        public byte ServiceID { get; } = 102;
    }
}
