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
 */

using System.Net.Sockets;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class SERVER_100_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 100;
    public string ProtocolType { get; } = "SERVER";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Server General Messages.";

    public class MSG_CREATEGAMESERVER : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 100;

        public string Name;
        public ushort Port;
        public string RealmName;

    }

    public class MSG_ALLOCATESOCKET : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 100;

        public Socket Socket;

    }

    public class MSG_DEALLOCATESOCKET : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 100;

        public ushort Id;
        public Socket Socket;
        public string Ip;

    }

    public class MSG_QUERYACTORFACTORY : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 100;

    }

    public class MSG_ACTORFACTORYINFO : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 100;

        public IActorRef Reference;

    }

    public class MSG_QUERYSERVER : IServerMessage {
        
        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 100;
        
    }

    public class MSG_GETBESTSERVER : IServerMessage {
        
        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 100;
        
    }

    public class MSG_PLAYERENQUEUED : IServerMessage {
        
        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 100;

        public SessionActor SessionActor;
        public ByteString Key;
        public bool VIPEntry;
        
    }

    public class MSG_SERVERINFO : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 100;

        public ByteString IP;
        public int Port;
        public ushort PlayerCount;
        public TcpListener TcpClient;
        public IActorRef ActorRef;
        public string[] ConnectedIps;
        public string RealmName;

    }

    public class MSG_CREATEKEY : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 100;

        public Account Account;

    }

    public class MSG_CREATEKEYRSP : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 100;

        public ByteString Key;

    }

    public class MSG_VALIDATESESSIONKEY : IServerMessage {
        
        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 100;

        public ByteString Key;
        public ulong UserID;
        public SessionActor SessionActor;
        
    }

    public class MSG_VALIDATESESSIONKEYRSP : IServerMessage {
        
        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 100;

        // 0: Success
        // 1: Failed
        // @todo: make these string IDs instead.
        public int ErrorCode;
        public Account Account;
        
    }

    public class MSG_PING : IServerMessage {
        
        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 100;

        public long Ping;
        
    }

    public class MSG_INITIALIZE : IServerMessage {
        
        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 100;
        
    }

    public class MSG_INITIALIZE_COMPLETE : IServerMessage {
        
        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 100;
        
    }

    public class MSG_COMMAND : IServerMessage {
        
        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 100;

        public WideByteString CommandText;
        public IActorRef ActorRef;
        public CoreObject CoreObject;
        public Wizard Wizard;
        public Account Account;
        public IActorRef ZoneActor;
        public IActorRef ServerActor;
        public Account SelectedAccount;
        public Wizard SelectedWizard;
        
    }

    public class MSG_COMMANDRSP : IServerMessage {
        
        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 100;

        public WideByteString CommandText;
        public bool Failed;
        public ByteString ResponseText;
        
    }

    public class MSG_PLAYERENQUEUEDRSP : IServerMessage {
        
        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 100;

        public int PositionInQueue;
        public int Status;
        public bool Failed;
        
    }

    public class MSG_FINDPLAYER : IServerMessage {
        
        public byte MessageOrder { get; } = 20;
        public byte ServiceID { get; } = 100;

        public ulong UserID;
        public string Username;
        public string CharacterName;
        public string Ip;
        
    }

    public class MSG_PLAYERFOUND : IServerMessage {
        
        public byte MessageOrder { get; } = 21;
        public byte ServiceID { get; } = 100;

        public bool Found;
        public IActorRef ServerActor;
        
    }

    public class MSG_KICKPLAYER : IServerMessage {
        
        public byte MessageOrder { get; } = 22;
        public byte ServiceID { get; } = 100;

        public ulong AccountID;
        
    }

    public class MSG_RECEIVEDPACKET : IServerMessage {
        
        public byte MessageOrder { get; } = 23;
        public byte ServiceID { get; } = 100;

        public IMessage Packet;
    }

    /// <summary>
    /// Sent to the GameServerPool to create a session key on a specific realm's game server.
    /// Used for cross-server realm transfers.
    /// </summary>
    public class MSG_CREATEPLAYERKEY : IServerMessage {

        public byte MessageOrder { get; } = 24;
        public byte ServiceID { get; } = 100;

        public Account Account;
        public string TargetRealmName;

    }

    public class MSG_CREATEPLAYERKEYRSP : IServerMessage {

        public byte MessageOrder { get; } = 25;
        public byte ServiceID { get; } = 100;

        public ByteString Key;
        public string IP;
        public ushort Port;
        public string RealmName;
        public bool Success;

    }

    /// <summary>
    /// Query the GameServerPool for info about a specific realm's game server.
    /// </summary>
    public class MSG_QUERYREALMSERVER : IServerMessage {

        public byte MessageOrder { get; } = 26;
        public byte ServiceID { get; } = 100;

        public string RealmName;

    }

    /// <summary>
    /// Returns the list of all realm names with their player counts.
    /// </summary>
    public class MSG_REALMLIST : IServerMessage {

        public byte MessageOrder { get; } = 27;
        public byte ServiceID { get; } = 100;

        public string[] RealmNames;
        public ushort[] PlayerCounts;
        public ushort[] PlayerLimits;

    }

    /// <summary>
    /// Internal aggregate carrying concurrent game-server query results back to
    /// GameServerPool for final "best server" selection.  Carries the original
    /// requester so the follow-up handler can reply to the correct session.
    /// </summary>
    public class MSG_QUERYGAMESERVER_AGGREGATE : IServerMessage {

        public byte MessageOrder { get; } = 28;
        public byte ServiceID { get; } = 100;

        public IActorRef OriginalSender;
        public MSG_SERVERINFO[] ServerInfos;

    }

    /// <summary>
    /// Internal aggregate carrying concurrent find-player query results back to
    /// GameServerPool so it can check whether the target IP is connected.
    /// </summary>
    public class MSG_FINDPLAYER_AGGREGATE : IServerMessage {

        public byte MessageOrder { get; } = 29;
        public byte ServiceID { get; } = 100;

        public IActorRef OriginalSender;
        public string TargetIp;
        public MSG_SERVERINFO[] ServerInfos;

    }

    /// <summary>
    /// Internal aggregate carrying concurrent realm-list query results back to
    /// GameServerPool for final assembly of the MSG_REALMLIST response.
    /// </summary>
    public class MSG_REALMLIST_AGGREGATE : IServerMessage {

        public byte MessageOrder { get; } = 30;
        public byte ServiceID { get; } = 100;

        public IActorRef OriginalSender;
        public string[] RealmNames;
        public ushort[] PlayerCounts;

    }
    
}
