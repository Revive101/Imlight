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

using System.Collections.Generic;
using System;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Game.Zone.Components;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;

namespace Imlight.CoreLib.Shared.Packets;

/// <summary>
/// Bitmask selecting which zone supervisors receive a broadcast.
/// </summary>
[Flags]
public enum ZoneBroadcastTarget : byte {
    None    = 0,
    Players = 1 << 0,
    Objects = 1 << 1,
    Volumes = 1 << 2,
    Triggers = 1 << 3,
    Paths   = 1 << 4,
    Sigils  = 1 << 5,
    All         = Players | Objects | Volumes | Triggers | Paths | Sigils,
    AllNoPlayers = All & ~Players,
}

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

        public string ZonePath;

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
        public PathTemplateList PathData;

        /// <summary>
        /// The data for the zone's node templates, as it appears in the game client data.
        /// </summary>
        public NodeTemplateList NodeData;

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

        public string ZonePath;

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
    /// Called and sent to a <see cref="Zone"/> to broadcast a message
    /// to the zone.  Carries an optional client-visible <see cref="IMessage"/>
    /// and/or optional server-internal <see cref="IServerMessage"/> array.
    /// </summary>
    public class MSG_ZONEBROADCAST : IServerMessage {

        public byte MessageOrder { get; } = 20;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The actor that sent the message.
        /// </summary>
        public IActorRef Sender;

        /// <summary>
        /// Client-visible message (e.g. movement, state change, wizbang).
        /// May be null when only server messages are present.
        /// </summary>
        public IMessage Message;

        /// <summary>
        /// Server-internal messages to forward to zone entities.
        /// May be null when only a client message is present.
        /// </summary>
        public IServerMessage[] Messages;

        /// <summary>
        /// True, if the message should not be sent to the sender.
        /// </summary>
        public bool Selfless;

        /// <summary>
        /// Which supervisors the zone should forward this message to.
        /// </summary>
        public ZoneBroadcastTarget Targets = ZoneBroadcastTarget.All;

    }

    /// <summary>
    /// Server-internal nudge telling EquipmentService to reconcile the equipped mount with the current
    /// zone: really unequip it on entering an interior and re-equip it on returning outdoors. Sent by
    /// ZoneService on zone entry. When <see cref="Force"/> is set the mount is stowed regardless of the
    /// zone's no-mounts flag (dungeon-sigil entry dismounts on a street pad); the plain reconcile still
    /// remounts on the way back out.
    /// </summary>
    public sealed class MSG_ENFORCEINTERIORMOUNT : IServerMessage {

        public byte MessageOrder { get; } = 21;
        public byte ServiceID { get; } = 102;

        public bool Force;

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

        /// <summary>
        /// A fresh dungeon-sigil entry starts a NEW run: GameWorld drops the owner's stale copy of the
        /// destination before routing the transfer, so a second entry rebuilds the instance instead of
        /// rejoining the old one. Never set by re-attach or recall transfers.
        /// </summary>
        public bool ResetInstance;

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
        public string ErrorMessage;
        public List<GID> CriticalObjects;

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

    public sealed class MSG_ZONEINTERACTION : IServerMessage {

        public byte MessageOrder { get; } = 38;
        public byte ServiceID { get; } = 102;

        public ulong GlobalID;
        public string ServiceName;
        public uint ServiceIndex;
        public IActorRef PlayerActor;
        public Wizard PlayerCharacter;
        public CoreObject PlayerObject;
        public int Reinteract = 0; 

    }

    public sealed class MSG_INSTANCECONTAINERHASZONE : IServerMessage {

        public byte MessageOrder { get; } = 39;
        public byte ServiceID { get; } = 102;

        public string ZoneName;

    }

    public sealed class MSG_INSTANCECONTAINERHASZONERSP : IServerMessage {

        public byte MessageOrder { get; } = 40;
        public byte ServiceID { get; } = 102;

        public bool HasZone;

    }

    public sealed class MSG_RANDOMFLIPS : IServerMessage {
        public byte MessageOrder { get; } = 41;
        public byte ServiceID { get; } = 102;
        public string ZoneName;
        public ulong SenderCharID;
    }

    public sealed class MSG_ZONEPATHSPAWN : IServerMessage {

        public byte MessageOrder { get; } = 42;
        public byte ServiceID { get; } = 102;

        public uint SpawnObjectID;
    }

    public sealed class MSG_ENTERSTATE : IServerMessage {

        public byte MessageOrder { get; } = 43;
        public byte ServiceID { get; } = 102;

        public string StateName;
        public string ObjectName;
        public bool ExclusiveToSender = false;
        public IActorRef Sender;

    }

    public sealed class MSG_REMOVEOBJECT : IServerMessage {

        public byte MessageOrder { get; } = 44;
        public byte ServiceID { get; } = 102;
        public string ObjectName;
        public ulong TemplateID;

    }

    public sealed class MSG_WIZBANGUPDATEINTERVAL : IServerMessage {

        public byte MessageOrder { get; } = 45;
        public byte ServiceID { get; } = 102;

    }

    public sealed class MSG_TRIGGERSEAMLESSTRANSITION : IServerMessage {

        public byte MessageOrder { get; } = 46;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public Wizard PlayerCharacter;
        public CoreObject PlayerObject;

    }

    public sealed class MSG_STARTSEAMLESSTRANSITION : IServerMessage {

        public byte MessageOrder { get; } = 47;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public Wizard PlayerCharacter;
        public CoreObject PlayerObject;

    }

    public sealed class MSG_PRELOGIN : IServerMessage {

        public byte MessageOrder { get; } = 48;
        public byte ServiceID { get; } = 102;

    }
    
    public sealed class MSG_REGISTERCRITICALOBJECT : IServerMessage {

        public byte MessageOrder { get; } = 49;
        public byte ServiceID { get; } = 102;

        public GID ObjectID;

    }

    
    public sealed class MSG_DELAYEDDELETEOBJECT : IServerMessage {

        public byte MessageOrder { get; } = 50;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// PipeTo aggregate carrying the result of an instance-container zone-exists query
    /// back to GameWorld for final processing.
    /// </summary>
    public sealed class MSG_INSTANCECONTAINER_QUERY_RESULT : IServerMessage {

        public byte MessageOrder { get; } = 51;
        public byte ServiceID { get; } = 102;

        public IActorRef OriginalSender;
        public MSG_INSTANCECONTAINERHASZONERSP Response;
        public MSG_ZONETRANSFER OriginalTransfer;

    }

    /// <summary>
    /// PipeTo aggregate carrying concurrent wizard-query results for a wizbang update tick.
    /// </summary>
    public sealed class MSG_WIZBANG_UPDATE_RESULT : IServerMessage {

        public byte MessageOrder { get; } = 52;
        public byte ServiceID { get; } = 102;

        public IActorRef[] PlayerActors;
        public Wizard[] Wizards;

    }

    /// <summary>
    /// Periodic timer tick that flushes batched player/creature moves to supervisors.
    /// </summary>
    public sealed class MSG_FLUSHMOVES : IServerMessage {

        public byte MessageOrder { get; } = 53;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Timer-fired message that releases a mobile ID back to the pool after a cooldown,
    /// preventing races between MSG_REMOVEOBJECT delivery and mobile ID reuse.
    /// </summary>
    public sealed class MSG_RELEASEMOBILEID : IServerMessage {

        public byte MessageOrder { get; } = 54;
        public byte ServiceID { get; } = 102;

        public ushort MobileId;

    }

    /// <summary>
    /// Sent by a session service to a <see cref="Zone"/> to fetch its loaded <see cref="WizZoneData"/>,
    /// e.g. to check zone flags like m_noMounts.
    /// </summary>
    public sealed class MSG_QUERYZONEDATA : IServerMessage {

        public byte MessageOrder { get; } = 55;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by a <see cref="Zone"/> in response to <see cref="MSG_QUERYZONEDATA"/>.
    /// </summary>
    public sealed class MSG_QUERYZONEDATARSP : IServerMessage {

        public byte MessageOrder { get; } = 56;
        public byte ServiceID { get; } = 102;

        public WizZoneData ZoneData;

    }

    /// <summary>
    /// Sent by a result handler to a <see cref="Zone"/> to find the dueling creature that has
    /// the given player in its aggro radius, e.g. for <c>ResInitiateCombat</c>. Any number of
    /// creatures may reply; the first reply wins.
    /// </summary>
    public sealed class MSG_QUERYNEARESTDUELTARGET : IServerMessage {

        public byte MessageOrder { get; } = 57;
        public byte ServiceID { get; } = 102;

        public CoreObject PlayerGameObject;

    }

    /// <summary>
    /// Sent by a dueling creature in response to <see cref="MSG_QUERYNEARESTDUELTARGET"/>.
    /// </summary>
    public sealed class MSG_QUERYNEARESTDUELTARGETRSP : IServerMessage {

        public byte MessageOrder { get; } = 58;
        public byte ServiceID { get; } = 102;

        public IActorRef CreatureActor;
        public CoreObject CreatureObject;

    }

    /// <summary>
    /// Server-internal: tells a duel component to spawn a summoned minion once the summon cast
    /// animation has played.
    /// </summary>
    public sealed class MSG_DEFERREDMINIONSUMMON : IServerMessage {

        public byte MessageOrder { get; } = 59;
        public byte ServiceID { get; } = 102;

        public uint CreatureTid;
        public CombatDuelSubCircle Caster;

    }

    /// <summary>
    /// Sent by a <see cref="Components.InteractDungeonSigilComponent"/> (zone actor) to the pressing
    /// player's <see cref="Services.ZoneService"/>: the player pressed X on a dungeon sigil and wants to
    /// enter. Carries everything the session needs to run the countdown and transfer, resolved by the
    /// pad entity from the zone's teleport data. Never sent over the wire.
    /// </summary>
    public sealed class MSG_STARTSIGILENTRY : IServerMessage {

        public byte MessageOrder { get; } = 60;
        public byte ServiceID { get; } = 102;

        /// <summary>The pad position, "X,Y,Z,heading" (heading = the pad's Z-orientation).</summary>
        public string SigilLoc;

        /// <summary>The pad OBJECT's global id, used to trip the client's native on-face countdown.</summary>
        public ulong SigilGID;

        /// <summary>The sigil template name (m_sigilType), used to resolve the sub-circle face slots.</summary>
        public string SigilType;

        /// <summary>The pad's detection radius (m_radius); the player must stay within it to enter.</summary>
        public float Radius;

        /// <summary>The instance zone the sigil leads to.</summary>
        public string DestinationZone;

        /// <summary>The arrival position inside the instance, "X,Y,Z,heading".</summary>
        public string DestinationLoc;

    }

    /// <summary>
    /// Delayed self-message a <see cref="Services.ZoneService"/> fires once a dungeon-sigil countdown
    /// completes: verifies the player is still on the pad, then transfers them into the instance.
    /// </summary>
    public sealed class MSG_SIGILENTER : IServerMessage {

        public byte MessageOrder { get; } = 61;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent by <see cref="World.GameWorld"/> to an <see cref="World.InstanceContainer"/>: drop the loaded
    /// copy of <see cref="ZoneName"/> (stop the zone actor) so the next transfer builds a fresh instance.
    /// </summary>
    public sealed class MSG_DROPINSTANCEZONE : IServerMessage {

        public byte MessageOrder { get; } = 62;
        public byte ServiceID { get; } = 102;

        public string ZoneName;

    }

}
