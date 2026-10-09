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
/// What a session claims on the instance it is in; the zone answers whether it is the first to do it.
/// </summary>
public enum InstanceQuestClaimKind : byte {
    QuestStart,
    GoalStart,
    GoalComplete,
    QuestComplete,
}

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
        public bool SuppressTeleportResults;

        /// <summary>
        /// Set when a player spawned inside the volume that raised the event instead of walking into it.
        /// </summary>
        public bool PlayerSpawned;

        /// <summary>
        /// Set by the trigger supervisor once it has checked the trigger's requirements, so the trigger does not
        /// check them again against state the same event has since changed.
        /// </summary>
        public bool RequirementsChecked;

        /// <summary>
        /// The adjectives of the monster a Monster_Killed event is about; requirements on the event read them.
        /// </summary>
        public IReadOnlyList<string> Adjectives;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a ResStartStagedCinematic result. Cutscenes are not played, so the zone
    /// raises the end event the triggers wait for at once.
    /// </summary>
    public class MSG_STARTSTAGEDCINEMATIC : IServerMessage {

        public byte MessageOrder { get; } = 69;
        public byte ServiceID { get; } = 102;

        public IActorRef PlayerActor;
        public CoreObject PlayerGameObject;

    }

    /// <summary>
    /// Sent to a player's session when an event was posted in the zone (or to the one player a quest result
    /// posts it for): active quest goals that list the event in their generic events complete.
    /// </summary>
    public class MSG_ZONEEVENTFORQUESTS : IServerMessage {

        public byte MessageOrder { get; } = 70;
        public byte ServiceID { get; } = 102;

        public string EventName;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a token, counter or puzzle-variable result: the zone keeps the change in its
    /// script state for the player who triggered it.
    /// </summary>
    public class MSG_ZONESCRIPTRESULT : IServerMessage {

        public byte MessageOrder { get; } = 71;
        public byte ServiceID { get; } = 102;

        public Imcodec.ObjectProperty.TypeCache.Result Result;
        public CoreObject PlayerGameObject;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a ResModifyTriggerObject result: the named object enters the state
    /// for every player in the zone, and the zone raises the object's EnterState event. A zone of an instance
    /// hands the message on to the instance's other zones when it does not hold the object itself, since the player may not be in the zone that holds it.
    /// </summary>
    public class MSG_MODIFYTRIGGEROBJECT : IServerMessage {

        public byte MessageOrder { get; } = 68;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// Whether the instance container already handed this message on, so the zone applies it and does not hand it on again.
        /// </summary>
        public bool Relayed;

        public string ObjectName;
        public string StateName;
        public IActorRef PlayerActor;
        public CoreObject PlayerGameObject;
        public bool PlayerSpawned;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a ResRemoveTriggerObject result: the object with that zone tag, trigger-owned
    /// or placed in the zone, is removed for every player in the zone.
    /// </summary>
    public class MSG_REMOVETRIGGEROBJECT : IServerMessage {

        public byte MessageOrder { get; } = 73;
        public byte ServiceID { get; } = 102;

        public string ObjectName;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a ResAddTriggerObject result: the placed object with that zone tag is
    /// brought back in the given state if a ResRemoveTriggerObject removed it.
    /// </summary>
    public class MSG_ADDTRIGGEROBJECT : IServerMessage {

        public byte MessageOrder { get; } = 79;
        public byte ServiceID { get; } = 102;

        public string ObjectName;
        public string StateName;

    }

    /// <summary>
    /// Sent by a <see cref="VolumeComponent"/> to a player's session when the player entered
    /// a quest-proximity volume and has one of that volume's goals active. The volume already
    /// matched the goal to itself; the session only needs to complete it.
    /// </summary>
    public sealed class MSG_COMPLETEPROXIMITYGOAL : IServerMessage {

        public byte MessageOrder { get; } = 63;
        public byte ServiceID { get; } = 102;

        /// <summary>
        /// The ID of the quest instance that owns the goal the player just completed.
        /// </summary>
        public ulong QuestID;

        /// <summary>
        /// The ID of the goal instance the player just completed by entering the volume.
        /// </summary>
        public ulong GoalID;

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

        // ResSpawn.m_activate: true switches the spawner on and spawns; false switches it off (no spawn).
        public bool Activate = true;

        // Spawn only when the spawner has nothing up (a character's remembered spawn coming back).
        public bool OnlyIfAbsent;

        // The player whose quest or trigger caused the spawn, if any. The spawned creature checks its
        // aggro radius against this player at once instead of waiting for the player's next step.
        public CoreObject PlayerObject;
        public IActorRef PlayerActor;
        public Wizard PlayerWizard;
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
    /// Requests the zone to spawn a new entity from a CoreObject + CoreTemplate.
    /// </summary>
    public sealed class MSG_SPAWNENTITY : IServerMessage {

        public byte MessageOrder { get; } = 65;
        public byte ServiceID { get; } = 102;

        public CoreObject CoreObject;
        public CoreTemplate Template;
        /// <summary>Optional: the player actor requesting the spawn (for routing).</summary>
        public IActorRef Requester;

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

    /// <summary>
    /// Timer-fired message to a zone supervisor or <see cref="ZonePath"/> that gives up on entities that
    /// never answered their <see cref="MSG_ZONEOBJECTLOADBEGIN"/>. <see cref="Entity"/> names one entity;
    /// null means every entity still loading.
    /// </summary>
    public sealed class MSG_ENTITYLOADTIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 64;
        public byte ServiceID { get; } = 102;

        public IActorRef Entity;

    }

    /// <summary>
    /// Response to MSG_SPAWNENTITY with the created entity actor reference.
    /// </summary>
    public sealed class MSG_SPAWNENTITYRSP : IServerMessage {

        public byte MessageOrder { get; } = 66;
        public byte ServiceID { get; } = 102;

        public IActorRef EntityActor;
        public CoreObject SpawnedObject;

    }

    /// <summary>
    /// Sent by an owner's EquipmentService to the zone's objects: the summoned pet whose world GID is
    /// <see cref="PetGlobalId"/> leaves the world for everyone in the zone.
    /// </summary>
    public sealed class MSG_DISMISSPET : IServerMessage {

        public byte MessageOrder { get; } = 67;
        public byte ServiceID { get; } = 102;

        public ulong PetGlobalId;

    }

    /// <summary>
    /// Timer-fired message to an instanced <see cref="Zone"/>: it has had no players for the configured
    /// idle time, so it asks its <see cref="InstanceContainer"/> to drop it.
    /// </summary>
    public sealed class MSG_INSTANCEIDLEEXPIRE : IServerMessage {

        public byte MessageOrder { get; } = 72;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a session that starts or completes a dungeon quest or goal. The zone records
    /// it as the instance's progress and answers with <see cref="MSG_CLAIMINSTANCEQUESTRSP"/>. A goal completion
    /// the zone has not seen yet is also told to every other player in the instance.
    /// </summary>
    public sealed class MSG_CLAIMINSTANCEQUEST : IServerMessage {

        public byte MessageOrder { get; } = 74;
        public byte ServiceID { get; } = 102;

        public InstanceQuestClaimKind Kind;
        public string QuestName;
        public string GoalName;
        public IActorRef Origin;

    }

    /// <summary>
    /// The zone's answer to <see cref="MSG_CLAIMINSTANCEQUEST"/>: whether the claim was the first for this
    /// instance, so the world effects of the quest step run only for it. Always true outside an instance.
    /// </summary>
    public sealed class MSG_CLAIMINSTANCEQUESTRSP : IServerMessage {

        public byte MessageOrder { get; } = 75;
        public byte ServiceID { get; } = 102;

        public bool First;

    }

    /// <summary>
    /// Sent to a <see cref="Zone"/> by a session that enters it, asking for the dungeon quest progress of the instance.
    /// </summary>
    public sealed class MSG_QUERYINSTANCEQUESTS : IServerMessage {

        public byte MessageOrder { get; } = 76;
        public byte ServiceID { get; } = 102;

    }

    /// <summary>
    /// The dungeon quest progress of an instance: the quests it finished and, per quest, the goals it completed
    /// in order.
    /// </summary>
    public sealed class MSG_QUERYINSTANCEQUESTSRSP : IServerMessage {

        public byte MessageOrder { get; } = 77;
        public byte ServiceID { get; } = 102;

        public bool IsInstance;
        public string[] CompletedQuests = [];
        public Dictionary<string, string[]> CompletedGoals = [];
        public string[] Zones = [];

    }

    /// <summary>
    /// Sent to every other session in an instance when a player completes a dungeon quest goal: the same goal
    /// completes for them.
    /// </summary>
    public sealed class MSG_INSTANCEGOALCOMPLETED : IServerMessage {

        public byte MessageOrder { get; } = 78;
        public byte ServiceID { get; } = 102;

        public IActorRef Origin;
        public string QuestName;
        public string GoalName;

    }

}
