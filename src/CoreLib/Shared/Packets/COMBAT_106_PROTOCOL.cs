/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Shared.Networking;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Packets {
    public sealed class COMBAT_106_PROTOCOL : IServerProtocol
	{
		public byte ServiceID => 106;
		public string ProtocolType => "Wizard dueling messages";
		public int ProtocolVersion => 1;
		public string ProtocolDescription => "Internal messages for dueling.";

		public sealed class MSG_STARTDUEL : IServerMessage
		{
			public byte MessageOrder => 1;
			public byte ServiceID => 106;

			public Dictionary<IActorRef, CoreObject> Participants;
			public ulong SigilId;
			public Vector3 SigilLocation;
			public Vector3 SigilOrientation;
            public CombatSigilTemplate SigilTemplate;
		}

		public sealed class MSG_ENDDUEL : IServerMessage
		{
			public byte MessageOrder => 2;
			public byte ServiceID => 106;

			public Dictionary<IActorRef, CoreObject> Participants;
		}

		public sealed class MSG_DUELDETAILS : IServerMessage
		{
			public byte MessageOrder => 3;
			public byte ServiceID => 106;

			public IActorRef DuelActor;
			public Duel Duel;
            public byte CreatureCount;
            public byte PlayerCount;
		}

        public sealed class MSG_ADDPARTICIPANT : IServerMessage
        {
            public byte MessageOrder => 4;
            public byte ServiceID => 106;

            public IActorRef Participant;
            public CoreObject ParticipantObject;
        }

        public sealed class MSG_GRACEPERIODOVER : IServerMessage
        {
            public byte MessageOrder => 5;
            public byte ServiceID => 106;
        }

        public sealed class MSG_SLOTAVAILABLE : IServerMessage
        {
            public byte MessageOrder => 6;
            public byte ServiceID => 106;

            public Team Team;
        }

        public sealed class MSG_SLOTAVAILABLERSP : IServerMessage
        {
            public byte MessageOrder => 7;
            public byte ServiceID => 106;

            public bool Available;
        }

        public sealed class MSG_NEWROUND : IServerMessage
        {
            public byte MessageOrder => 8;
            public byte ServiceID => 106;

            public int Round;
        }

        public sealed class MSG_ACTORADDEDTODUEL : IServerMessage
        {
            public byte MessageOrder => 9;
            public byte ServiceID => 106;

            public IActorRef DuelActor;
            public Vector3 SlotPosition;
            public float SlotOrientation;
        }

        public sealed class MSG_ACTORCOMBATMOVE : IServerMessage
        {
            public byte MessageOrder => 10;
            public byte ServiceID => 106;

            public IActorRef Actor;
            public byte MoveType;
            public byte SpellSelection;
            public uint SpellTarget;
            public int TimeLeft;
        }

        public sealed class MSG_ROUNDOVER : IServerMessage
        {
            public byte MessageOrder => 11;
            public byte ServiceID => 106;
        }
	}
}
