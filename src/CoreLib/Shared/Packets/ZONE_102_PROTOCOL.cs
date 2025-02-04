/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.IO;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.Game.Zone.Components;

namespace Imlight.CoreLib.Shared.Packets;

public class ZONE_102_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 102;
    public string ProtocolType { get; } = "ZONE";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Zone General Messages."; 

    /// <summary>
    /// Sent by the <see cref="Zone"/> to a <see cref="ZoneLoader"/> to begin loading a zone.
    /// </summary>
    public sealed class MSG_ZONELOADBEGIN : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The path to the zone file as it would appear in the <see cref="AccessPassManager"/>.
        /// </summary>
        public string ZonePath;

    }

    /// <summary>
    /// Sent by the <see cref="ZoneLoader"/> to the <see cref="Zone"/> to indicate that the zone has been loaded.
    /// </summary>
    public sealed class MSG_ZONELOADRESULTS : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The data for the zone, as it appears in the game client data.
        /// </summary>
        public WizZoneData ZoneData;

        /// <summary>
        /// The data for the zone's spawn points, as it appears in the game client data.
        /// </summary>
        public SpawnManager SpawnData;

        /// <summary>
        /// The data for the zone's paths, as it appears in the game client data.
        /// </summary>
        public PathManager_PathTemplateList PathData;

        /// <summary>
        /// The data for the zone's node templates, as it appears in the game client data.
        /// </summary>
        public PathManager_NodeTemplateList NodeData;

        /// <summary>
        /// The data for the zone's volumes, as it appears in the game client data.
        /// </summary>
        public WizZoneVolumes VolumeData;

        /// <summary>
        /// The data for the zone's triggers, as it appears in the game client data.
        /// </summary>
        public WizZoneTriggers TriggerData;

        /// <summary>
        /// True, if an error occured during loading.
        /// </summary>
        public bool Error;

        /// <summary>
        /// The error message, if an error occured during loading.
        /// </summary>
        public string ErrorMessage;

    }

    /// <summary>
    /// Sent by the <see cref="Zone"/> to itself to indicate that the zone load timer has expired.
    /// This message is used to ensure that the zone is loaded within a certain time frame.
    /// </summary>
    public sealed class MSG_ZONELOADTIMER : IServerMessage {

        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneSupervisor"/> to the <see cref="Zone"/> to indicate that the zone supervisor has been loaded.
    /// </summary>
    public sealed class MSG_ZONESUPERVISORLOADRESULTS : IServerMessage {

        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The name of the supervisor that has finished loading.
        /// </summary>
        public string SupervisorName;

        /// <summary>
        /// True, if an error occured during loading.
        /// </summary>
        public bool Error;

        /// <summary>
        /// The error message, if an error occured during loading.
        /// </summary>
        public string ErrorMessage;

    }

    /// <summary>
    /// Sent by a <<see cref="ZoneSupervisor"/> to a <see cref="ZoneEntity"/> to indicate that the zone object is being loaded.
    /// </summary>
    public sealed class MSG_ZONEOBJECTLOADBEGIN : IServerMessage {

        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneEntity"/> to the <see cref="ZoneSupervisor"/> to indicate that the zone object has been loaded.
    /// </summary>
    public sealed class MSG_ZONEOBJECTLOADRESULTS : IServerMessage {

        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// True, if an error occured during loading.
        /// </summary>
        public bool Error;

        /// <summary>
        /// The error message, if an error occured during loading.
        /// </summary>
        public string ErrorMessage;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneEntity"/> to a <see cref="ZoneEntityComponent"/> actor to request the identity of the entity.
    /// The identity is usually the type.
    /// </summary>
    public sealed class MSG_ENTITYCOMPONENTREQUESTIDENTITY : IServerMessage {

        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneEntityComponent"/> actor to a <see cref="ZoneEntity"/> to respond to a request for the entity's identity.
    /// </summary>
    public sealed class MSG_ENTITYCOMPONENTREQUESTIDENTITYRSP : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The identity of the entity.
        /// </summary>
        public ZoneEntityComponent Component;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneEntity"/> to a <see cref="Zone"/> to get a unique mobile ID.
    /// </summary>
    public sealed class MSG_GETRESERVEDMOBILEID : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by the <see cref="Zone"/> to a <see cref="ZoneEntity"/> to respond to a request for a unique mobile ID.
    /// </summary>
    public sealed class MSG_GETRESERVEDMOBILEIDRSP : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 102;

        public ushort MobileID;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> to find an object by its global ID or mobile ID.
    /// </summary>
    public sealed class MSG_QUERYZONEENTITY : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The global ID of the object to find.
        /// </summary>
        public ulong GlobalID;

        /// <summary>
        /// The mobile ID of the object to find.
        /// </summary>
        public ushort MobileID;

    }

    /// <summary>
    /// Sent by a <see cref="Zone"/> to respond to a request to find an object by its global ID or mobile ID.
    /// </summary>
    public sealed class MSG_QUERYZONEENTITYRSP : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// True, if the object was found.
        /// </summary>
        public bool Found;

        /// <summary>
        /// The object that was found.
        /// </summary>
        public ZoneEntity ZoneObject;

    }

    /// <summary>
    /// Called by a <see cref="Zone"/> to all <see cref="ZoneEntitySupervisor"/>s to indicate that the
    /// zone has completed initialization and is now starting.
    /// </summary>
    public sealed class MSG_ZONESTART : IServerMessage {
        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 102;
    }

    /// <summary>
    /// Called by a <see cref="ZoneService"/> to a <see cref="Zone"/> to indicate the player needs to be
    /// added to the zone.
    /// </summary>
    public class MSG_ADDPLAYER : IServerMessage {

        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public CoreObject PlayerObject;
        public Wizard Wizard;
        public string ActualWizardName;

    }

    /// <summary>
    /// Called by a <see cref="Zone"/> to a <see cref="ZoneService"/> to indicate that the player has been added to the zone.
    /// </summary>
    public class MSG_ADDPLAYERRSP : IServerMessage {

        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 102;

        public CoreObject WizardGameObject;

    }

    /// <summary>
    /// Called by a <see cref="ZoneService"/> to a <see cref="Zone"/> to indicate that the player needs to be removed from the zone.
    /// </summary>
    public class MSG_REMOVEPLAYER : IServerMessage {

        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public ulong GlobalId;
        public ushort MobileId;
        public bool IsPlayerStillConnected;

    }

    /// <summary>
    /// Called by a <see cref="Zone"/> to a <see cref="ZoneService"/> to indicate that the player has been removed from the zone.
    /// </summary>
    public class MSG_REMOVEPLAYERRSP : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Called by a <see cref="Zone"/> to all currently connected players to indicate that a player (that is not them)
    /// has been added to the zone.
    /// </summary>
    public class MSG_PLAYERADDEDTOZONE : IServerMessage {

        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public CoreObject PlayerObject;

    }

    /// <summary>
    /// Called by a <see cref="Zone"/> to all currently connected players to indicate that a player (that is not them)
    /// has been removed from the zone.
    /// </summary>
    public class MSG_PLAYERREMOVEDFROMZONE : IServerMessage {

        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public ulong GlobalId;

    }

    /// <summary>
    /// Called and sent to a <see cref="Zone"/> to broadcast a particular message
    /// to other <see cref="SessionActor"/>s within that zone.
    /// </summary>
    public class MSG_ZONEBROADCAST : IServerMessage {

        public byte MessageOrder { get; } = 20;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The actor that sent the message.
        /// </summary>
        public IActorRef Sender;

        /// <summary>
        /// The message to broadcast.
        /// </summary>
        public IMessage Message;

        /// <summary>
        /// True, if the message should not be sent to the sender.
        /// </summary>
        public bool Selfless;

    }

    /// <summary>
    /// Called and sent to a <see cref="Zone"/> to broadcast a particular message
    /// to all <see cref="ZoneEntity"/>s within that zone.
    /// </summary>
    public class MSG_ZONESUPERVISORBROADCAST : IServerMessage {

        public byte MessageOrder { get; } = 21;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The actor that sent the message.
        /// </summary>
        public IActorRef Sender;

        /// <summary>
        /// The messages to broadcast.
        /// </summary>
        public IServerMessage[] Messages;

    }

    /// <summary>
    /// Called by a <see cref="ZonePath"/> to itself to indicate that it's time to spawn a creature,
    /// if possible.
    /// </summary>
    public sealed class MSG_PATHSPAWNINTERVAL : IServerMessage {

        public byte MessageOrder { get; } = 22;
        public byte ServiceID { get; } = 102;

        public SpawnObject SpawnObject;
    }

    /// <summary>
    /// Called by a <see cref="PathMovementComponent"/> to itself to indicate that it's time to move a creature.
    /// </summary>
    public sealed class MSG_CREATUREMOVEINTERVAL : IServerMessage {

        public byte MessageOrder { get; } = 23;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneVolumeSupervisor"/> to a <see cref="VolumeComponent"/> to indicate
    /// the details of the volume.
    /// </summary>
    public sealed class MSG_VOLUMEDETAILS : IServerMessage {

        public byte MessageOrder { get; } = 24;
        public byte ServiceID { get; } = 102;

        public CoreObject CoreObject;
        public Volume Volume;

    }

    /// <summary>
    /// Sent by a <see cref="ZonePath"/> to a <see cref="PathMovementComponent"/> to indicate
    /// the details of the path.
    /// </summary>
    public sealed class MSG_PATHDETAILS : IServerMessage {

        public byte MessageOrder { get; } = 25;
        public byte ServiceID { get; } = 102;

        public List<NodeObject> NodeObjects;

    }

    /// <summary>
    /// Sent by a <see cref="ZoneSigilSupervisor"/> to a <see cref="CombatDuelComponent"/> to indicate
    /// the details of the sigil.
    /// </summary>
    internal sealed class MSG_SIGILDETAILS : IServerMessage {

        public byte MessageOrder { get; } = 26;
        public byte ServiceID { get; } = 102;

        public CombatSigilObjectInfo CombatSigilObjectInfo;

    }

    /// <summary>
    /// Called by a <see cref="ZoneEntity"/> to all its components to indicate the entity has finished initializing.
    /// </summary>
    public sealed class MSG_ZONEOBJECTINITIALIZED : IServerMessage {

        public byte MessageOrder { get; } = 27;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Called by a <see cref="NpcComponent"/> to a <see cref="Zone"/> to request a combat sigil.
    /// </summary>
    public class MSG_REQUESTCOMBATSIGIL : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 102;

        public Dictionary<IActorRef, CoreObject> StartingParticipants;

    }

    /// <summary>
    /// Called by a <see cref="PathMovementComponent"/> to itself to indicate the interval to check for interactions.
    /// </summary>
    public sealed class MSG_CREATUREFISHINTERACTIONINTERVAL : IServerMessage {

        public byte MessageOrder { get; } = 28;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> to indicate a posted event.
    /// </summary>
    public class MSG_POSTEVENT : IServerMessage {

        public byte MessageOrder { get; } = 29;
        public byte ServiceID { get; } = 102;

        public ByteString EventName;
        public IActorRef PlayerActor;
        public CoreObject? PlayerGameObject;

    }

    /// <summary>
    /// Sent by a <see cref="SessionActor"/> to a <see cref="Zone"/> to request a zone transfer.
    /// </summary>
    public class MSG_ZONETRANSFER : IServerMessage {

        public byte MessageOrder { get; } = 30;
        public byte ServiceID { get; } = 102;

        public string DestinationZone;
        public string DestinationLocation;
        public bool SendToClient = true;
        public bool IsPrivate = false;
        public ulong OwnerCharId;

    }

    /// <summary>
    /// Sent by a <see cref="Zone"/> to a <see cref="SessionActor"/> to respond to a zone transfer request.
    /// </summary>
    public class MSG_ZONETRANSFERRSP : IServerMessage {

        public byte MessageOrder { get; } = 31;
        public byte ServiceID { get; } = 102;

        public IActorRef ZoneActorRef;
        public ushort MobileId;
        public uint DynamicZoneId;
        public string ZoneDisplayName;
        public Vector3 Location;
        public float Orientation;
        public uint ErrorCode;

    } 

    /// <summary>
    /// Sent by a <see cref="MoveService"/> to a <see cref="Zone"/> to indicate the player is moving.
    /// </summary>
    public class MSG_PLAYERMOVE : IServerMessage {

        public byte MessageOrder { get; } = 32;
        public byte ServiceID { get; } = 102;

        public CoreObject PlayerObject;
        public IActorRef PlayerActor;
        public Wizard PlayerWizard;

    }

    /// <summary>
    /// Sent by a <see cref="PathMovementComponent"/> to a <see cref="Zone"/> to indicate the creature is moving.
    public sealed class MSG_CREATUREMOVE : IServerMessage {

        public byte MessageOrder { get; } = 33;
        public byte ServiceID { get; } = 102;

        public CoreObject CreatureObject;
        public IActorRef CreatureActor;
        public ZoneEntity CreatureEntity;

    }

    /// <summary>
    /// Sent by a <see cref="MoveService"/> to itself to indicate that it's time to fish for interactions.
    /// </summary>
    public class MSG_PLAYERMOVEINTERVAL : IServerMessage {

        public byte MessageOrder { get; } = 34;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Called by a <see cref="CombatDuelComponent"/> to a <see cref="SessionActor"/> that they have fled
    /// or have been defeated in a combat duel and need to be sent back to the hub zone.
    /// </summary>
    public class MSG_SENDTOHUB : IServerMessage {

        public byte MessageOrder { get; } = 35;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="Zone"/> to all <see cref="ZoneEntitySupervisor"/>s and <see cref="SessionActor"/>s that the zone
    /// is closing.
    /// </summary>
    public sealed class MSG_ZONECLOSED : IServerMessage {

        public byte MessageOrder { get; } = 36;
        public byte ServiceID { get; } = 102;

        public uint DynamicZoneId;

    }

    /// <summary>
    /// Sent by a <see cref="ZonePlayerSupervisor"/> to itself to indicate that all players need to be healed.
    /// </summary>
    public sealed class MSG_ZONEHEALTICK : IServerMessage {

        public byte MessageOrder { get; } = 37;
        public byte ServiceID { get; } = 102;

        public float MaxHealthPercent;

    }

}
