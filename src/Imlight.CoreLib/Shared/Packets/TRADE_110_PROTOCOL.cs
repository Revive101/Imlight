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
 * TRADE SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Internal messages between a session's TradeService and the process-wide
 * TradeDirectory, which owns every treasure card trade across zones and realms.
 * 
 * USAGE EXAMPLE:
 * TradeDirectory.Instance.Tell(new TRADE_110_PROTOCOL.MSG_READYCHANGED { CharId = wizard.CharId });
 * 
 * NOTE:
 * Every ID in this protocol is a saved character ID unless the field name
 * says otherwise. Client-visible replies are generated wire messages sent
 * straight to the participant's SessionActor. The cards of a swap never
 * travel in these messages: both sides read them from the TradeEscrow document.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Akka.Actor;
using Imlight.CoreLib.Game.Trading;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class TRADE_110_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 110;
    public string ProtocolType { get; } = "TRADE";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Trade Messages.";

    /// <summary>
    /// A requester pressed Trade on a target who is online in the same zone.
    /// </summary>
    public sealed class MSG_OPENTRADE : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 110;

        public IActorRef Session;
        public IActorRef Zone;
        public ulong RequesterCharId;
        public ulong TargetCharId;
        public string TargetSessionPath;

    }

    /// <summary>
    /// The directory asking the invitee's session whether it accepts an invite.
    /// </summary>
    public sealed class MSG_TRADEINVITE : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;
        public ulong RequesterCharId;
        public IActorRef RequesterZone;

    }

    /// <summary>
    /// The invitee's answer: delivered, or the error to show the requester.
    /// </summary>
    public sealed class MSG_INVITESCREENED : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;
        public ulong RequesterCharId;
        public IActorRef Session;
        public IActorRef Zone;
        public bool Delivered;
        internal TradeError Error;

    }

    public sealed class MSG_JOINCHANGED : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 110;

        public ulong CharId;
        public IActorRef Session;
        public IActorRef Zone;
        public ulong PeerCharId;

        internal TradeJoinStatus Join;

    }

    public sealed class MSG_READYCHANGED : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 110;

        public ulong CharId;
        public ulong PeerCharId;

        internal TradeReadyStatus Ready;

    }

    public sealed class MSG_OFFERCHANGED : IServerMessage {

        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 110;

        public ulong CharId;
        public ulong PeerCharId;
        public uint TemplateId;
        public uint Enchant;

        public byte Action;
        public int OwnedCount;

    }

    /// <summary>
    /// A session ending, or leaving the zone, while it may be part of a trade.
    /// </summary>
    public sealed class MSG_PARTICIPANTDEPARTED : IServerMessage {

        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 110;

        public ulong CharId;
        public IActorRef Session;
        public bool IsLogout;

    }

    /// <summary>
    /// Tells a session to lock its player's movement for a trade, or release it.
    /// </summary>
    public sealed class MSG_SETTRADESTATE : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 110;

        public bool Trading;

    }

    public sealed class MSG_TRADESTEP : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;
        internal TradeStep Step;

    }

    public sealed class MSG_TRADESTEPDONE : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;
        public ulong CharId;
        internal TradeStep Step;
        internal TradeError Error;

    }

    public sealed class MSG_INVITEEXPIRED : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;

    }

    public sealed class MSG_APPLYTIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 110;

        public Guid TradeId;

    }

}
