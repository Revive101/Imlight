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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * Internal messages between a session's PvPService and the process-wide PvPDirector, which owns the
 * matchmaking queue and every practice match across zones and realms.
 * 
 * USAGE EXAMPLE:
 * PvPDirector.Instance.Tell(new PVP_111_PROTOCOL.MSG_LEAVEQUEUE { CharId = wizard.CharId });
 * 
 * NOTE:
 * Every ID in this protocol is a saved character ID unless the field name says otherwise. Client-visible
 * replies are generated wire messages the director sends straight to the player's SessionActor. The messages
 * sent to a session (ENTERMATCH, MATCHJOINED, LEAVEARENA) carry what only that session can act on.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.PvP.Ranking;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class PVP_111_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 111;
    public string ProtocolType { get; } = "PVP";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal PvP Messages.";

    /// <summary>
    /// A session announcing (or refreshing) its character once the client is in the world.
    /// </summary>
    public sealed class MSG_PLAYERPRESENT : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 111;

        public IActorRef Session;
        internal PvPPlayerProfile Profile;
        public CoreObject PlayerObject;

    }

    /// <summary>
    /// A session ending. Logouts leave the queue at once; anything else waits out a grace period.
    /// </summary>
    public sealed class MSG_PLAYERDEPARTED : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public IActorRef Session;
        public bool IsLogout;

    }

    /// <summary>
    /// A player asking to queue for a match. The ids are the ones the client sent, already checked against the
    /// loaded tournament, match, league and season templates.
    /// </summary>
    public sealed class MSG_JOINQUEUE : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public uint TournamentNameId;
        public uint MatchNameId;
        public uint LeagueId;
        public uint SeasonId;
        public bool ExpandSearch;

        /// <summary>
        /// The rating of the player when the window is a ranked one; null for practice.
        /// </summary>
        internal PvPRatingSnapshot Ranked;

    }

    public sealed class MSG_LEAVEQUEUE : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 111;

        public ulong CharId;

    }

    /// <summary>
    /// A player answering the match found prompt.
    /// </summary>
    public sealed class MSG_CONFIRMMATCH : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public uint TournamentNameId;
        public bool Accept;

    }

    /// <summary>
    /// Sent to a lobby session once every player accepted: it transfers into the arena instance.
    /// </summary>
    public sealed class MSG_ENTERMATCH : IServerMessage {

        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 111;

        public ulong MatchId;
        public string ArenaZone;
        public ulong InstanceOwnerId;

    }

    /// <summary>
    /// Sent to the session that arrived in the arena, with the place the player returns to.
    /// </summary>
    public sealed class MSG_MATCHJOINED : IServerMessage {

        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 111;

        public ulong MatchId;
        public string ReturnZone;
        public string ReturnZoneDisplayName;
        public string ReturnLocation;

    }

    /// <summary>
    /// Sent by the arena duel to the director when the match is decided.
    /// </summary>
    public sealed class MSG_MATCHENDED : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 111;

        public ulong MatchId;
        public int WinningTeam;
        public IReadOnlyCollection<ulong> ForfeitedCharIds = [];

    }

    /// <summary>
    /// Tells the director a match cannot go ahead.
    /// </summary>
    public sealed class MSG_MATCHCANCELLED : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 111;

        public ulong MatchId;
        internal PvPCancelReason Reason;

    }

    /// <summary>
    /// Sent to a session in the arena once the match is over: it transfers back to where the player came from.
    /// </summary>
    public sealed class MSG_LEAVEARENA : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 111;

        public string ReturnZone;
        public string ReturnLocation;

    }

    /// <summary>
    /// Sent by a kiosk to the session of the player who used it: the next window the session may open is the one
    /// of this league.
    /// </summary>
    public sealed class MSG_KIOSKUSED : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 111;

        public uint LeagueId;

    }

    /// <summary>
    /// A queued player choosing whether matchmaking may search outside their skill range.
    /// </summary>
    public sealed class MSG_SETEXPANDSEARCH : IServerMessage {

        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public bool Expand;

    }

    /// <summary>
    /// A timer message of the director: look for pairs in the queue.
    /// </summary>
    public sealed class MSG_MATCHMAKINGTICK : IServerMessage {

        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 111;

    }

    /// <summary>
    /// A timer message of the director: a player departed without logging out and did not return in time.
    /// </summary>
    public sealed class MSG_DEPARTUREGRACEEXPIRED : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 111;

        public ulong CharId;

    }

    /// <summary>
    /// A player asking to host a practice match. The ids are the ones the client sent, already checked against the
    /// loaded tournament, match, league and season templates.
    /// </summary>
    public sealed class MSG_HOSTMATCH : IServerMessage {

        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public uint TournamentNameId;
        public uint MatchNameId;
        public uint LeagueId;
        public uint SeasonId;
        public PvPMatchRequest Request;

    }

    /// <summary>
    /// A player asking to join the hosted match with this id. The league and season are the ones of the window. The
    /// team id is the one of the team the player clicked; the server picks the slot in it.
    /// </summary>
    public sealed class MSG_JOINHOSTED : IServerMessage {

        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public ulong MatchId;
        public uint TournamentNameId;
        public uint LeagueId;
        public uint SeasonId;
        public ulong TeamId;

    }

    /// <summary>
    /// A session telling the director that its client shows the list of this tournament, so list changes are pushed to it.
    /// </summary>
    public sealed class MSG_WINDOWOPENED : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 111;

        public ulong CharId;
        public uint TournamentNameId;
        public uint LeagueId;
        public uint SeasonId;

    }

    /// <summary>
    /// Sent to the session of a player when a ranked match is decided: the session applies the result to the rating
    /// of the league and season.
    /// </summary>
    public sealed class MSG_RATEDRESULT : IServerMessage {

        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 111;

        public ulong MatchId;
        public uint LeagueId;
        public uint SeasonId;

        /// <summary>
        /// The rating of the opponent, or the average of the opposing team.
        /// </summary>
        public double OpponentElo;

        /// <summary>
        /// 1 for a win, 0.5 for a draw and 0 for a loss or a forfeit.
        /// </summary>
        public double Score;

    }

    /// <summary>
    /// Sent by the PvP commands: the rating service runs the action on the rating records of the player and replies
    /// with a server message.
    /// </summary>
    public sealed class MSG_RATINGCOMMAND : IServerMessage {

        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 111;

        /// <summary>
        /// One of: show, set, rankset, seasonreset, clear.
        /// </summary>
        public string Action;

        /// <summary>
        /// The rating for set, the rank index or name for rankset; empty otherwise.
        /// </summary>
        public string Value;

        /// <summary>
        /// The league type name; empty means the first rated league the player may use.
        /// </summary>
        public string League;

    }

}
