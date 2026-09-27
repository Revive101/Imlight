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
 *
 * ========================================================================
 * GROUP SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Internal messages between a session's GroupService and the process-wide
 * GroupDirectory, which owns every group across zones and realms.
 * 
 * USAGE EXAMPLE:
 * GroupDirectory.Instance.Tell(new GROUP_109_PROTOCOL.MSG_LEAVEGROUP { CharId = wizard.CharId });
 * 
 * NOTE:
 * Every ID in this protocol is a saved character ID unless the field name
 * says otherwise. Client-visible replies are generated wire messages sent
 * straight to the member's SessionActor.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class GROUP_109_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 109;
    public string ProtocolType { get; } = "GROUP";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Group Messages.";

    /// <summary>
    /// A session announcing (or refreshing) its character once the client is in the world.
    /// </summary>
    public sealed class MSG_MEMBERPRESENT : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 109;

        public IActorRef Session;
        internal GroupMemberProfile Profile;

    }

    /// <summary>
    /// A session ending. Logouts leave the group at once; anything else waits out a grace period.
    /// </summary>
    public sealed class MSG_MEMBERDEPARTED : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public IActorRef Session;
        public bool IsLogout;

    }

    public sealed class MSG_INVITEMEMBER : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 109;

        public ulong InviterCharId;

        /// <summary>
        /// Possible character IDs for the invitee, best first. The client mixes object and
        /// character IDs in the invite, so the directory picks the first one that is online.
        /// </summary>
        public IReadOnlyList<ulong> TargetCandidateIds;

        /// <summary>
        /// Characters that ignore the inviter; inviting them fails as if they were unavailable.
        /// </summary>
        public IReadOnlyCollection<ulong> CharIdsIgnoringInviter;

    }

    public sealed class MSG_INVITEREPLY : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public ulong GroupId;
        public bool Accept;

    }

    public sealed class MSG_LEAVEGROUP : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 109;

        public ulong CharId;

    }

    public sealed class MSG_REMOVEMEMBER : IServerMessage {

        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 109;

        public ulong RequesterCharId;
        public ulong TargetCharId;

        /// <summary>
        /// When non-zero, the request only applies if it names the requester's current group.
        /// </summary>
        public ulong GroupId;

        /// <summary>
        /// Staff removals skip the leader check.
        /// </summary>
        public bool IsStaffRequest;

    }

    public sealed class MSG_CHANGELEADER : IServerMessage {

        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 109;

        public ulong RequesterCharId;
        public ulong NewLeaderCharId;
        public bool IsStaffRequest;

    }

    public sealed class MSG_DISBANDGROUP : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 109;

        public ulong RequesterCharId;
        public bool IsStaffRequest;

    }

    public sealed class MSG_GROUPCHAT : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 109;

        public ulong SenderCharId;
        public ulong ChannelId;
        public ByteString SourceName;
        public WideByteString Message;

        /// <summary>
        /// Members who ignore the sender and must not receive the line.
        /// </summary>
        public IReadOnlyCollection<ulong> CharIdsIgnoringSender;

    }

    public sealed class MSG_SETSIGILSLOT : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public uint SigilSlot;

    }

    /// <summary>
    /// Raised inside a session by WizardService whenever the active wizard's level changes.
    /// </summary>
    public sealed class MSG_LEVELCHANGED : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public uint NewLevel;

    }

    public sealed class MSG_INVITEEXPIRED : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 109;

        public ulong TargetCharId;
        public ulong GroupId;

    }

    public sealed class MSG_DEPARTUREGRACEEXPIRED : IServerMessage {

        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 109;

        public ulong CharId;

    }

    /// <summary>
    /// The quest a member's quest helper tracks. Only the leader's is shared with the group; the
    /// client's serialized quest object in <see cref="QuestData"/> is relayed untouched.
    /// </summary>
    public sealed class MSG_SETGROUPQUEST : IServerMessage {

        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 109;

        public ulong RequesterCharId;
        public ulong QuestId;
        public ulong GoalId;
        public ByteString QuestData;

    }

    public sealed class MSG_SETGROUPFOLLOW : IServerMessage {

        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public bool Follow;

    }

    public sealed class MSG_SUMMONGROUP : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 109;

        public ulong RequesterCharId;

    }

    /// <summary>
    /// A group mate accepted the prompt to join a fight; sent to every sigil in their zone, and the
    /// sigil holding a circle for them pulls them in.
    /// </summary>
    public sealed class MSG_JOINRESERVEDDUEL : IServerMessage {

        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 109;

        public ulong CharId;
        public IActorRef PlayerActor;
        public CoreObject PlayerObject;

    }

}
