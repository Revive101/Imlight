/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty;
using Imlight.Common.IO;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.TypeCache.Duel;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.Common.ObjectProperty.PropertyReflection;
using System.Threading.Tasks;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Represents a duel. This is the actor that manages the duel. It is created by the
/// <see cref="DuelActorSupervisor"/> and is a child of it.
/// </summary>
public class DuelActor : ReceiveProtocolDispatcher, IWithTimers {
    public const float DuelRadius = 584.0f;
    public const byte NumOfSubCircles = 8;
    public const byte PlanningTime = 30;
    public const float AngleBetweenSubCircles = 36f;
    public const float FirstSubCircleAngle = 144f;
    private const float DuelStartedGracePeriodInSeconds = 3.75f;
    private const float YawErrorCompensation = 1.58f;
    private const int DelayAfterCombatAddInMs = 0;

    public ITimerScheduler Timers { get; set; }
    public Duel Duel { get; private set; }
    public ulong SigilId { get; private set; }

    private readonly IActorRef _wizardZoneRef;
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
        .OnBehaviors(SerializerOptions.Behaviors.None);
    private readonly SerializerOptions.PropertyFlags _combatParticipantFlags = (SerializerOptions.PropertyFlags) 4;
    private readonly SerializerOptions.PropertyFlags _combatParticipantStatFlags = (SerializerOptions.PropertyFlags) 5;
    private readonly SerializerOptions.PropertyFlags _combatParticipantHandFlags = (SerializerOptions.PropertyFlags) 5;

    private byte PlayerCount => (byte) _subCircles.Count(x => x.Occupied && x.OccupiedTeam == Team.Player);
    private byte CreatureCount => (byte) _subCircles.Count(x => x.Occupied && x.OccupiedTeam == Team.Monster);
    private DuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();

    private CombatSigilTemplate _combatSigilTemplate;
    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];
    private Vector3 _sigilLocation;
    private Vector3 _sigilOrientation;
    private byte _creatureCount;
    private byte _playerCount;

    public DuelActor(IActorRef wizardZoneRef) {
        this._wizardZoneRef = wizardZoneRef;
    }

    public static Props Props(IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new DuelActor(wizardZoneRef));

    internal void DuelBroadcast(IMessage message) {
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Selfless = false,
            Sender = Self,
            Message = message
        };
        _wizardZoneRef.Tell(broadcastMsg);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
        // Remember that a duel is *not* a sigil. A sigil is a physical object in the world.
        // A duel is a virtual object that is assigned to a sigil.
        // When this is called, it means a sigil has been activated and a duel is about to start.
        this._combatSigilTemplate = message.SigilTemplate;
        this.SigilId = message.SigilId;
        this._sigilLocation = message.SigilLocation;
        this._sigilOrientation = message.SigilOrientation;

        // Create duel object and the 8 subcircles.
        Duel = CreateDuelWithDefaults(message.SigilId);
        _subCircles = CreateDuelActorSubCircles(message.SigilTemplate);

        // Return the duel details to the sender. This is sent back to the sigil that requested the duel.
        var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
            DuelActor = Self,
            Duel = Duel
        };
        Sender.Tell(rsp);

        // When the duel is created, it must be created by two suspects: the player and the creature.
        // The creatures will always be team A and the players will always be team B. Assign the first
        // participants to their respective sub circles.
        var startingCreatureActor = message.Participants.Keys.Last();
        var startingPlayerActor = message.Participants.Keys.First();
        var startingCreatureObject = message.Participants.Values.Last();
        var startingPlayerObject = message.Participants.Values.First();

        var availableCreatureSubcircles = GetAvailableSubCircleTeamCreature();
        var availablePlayerSubcircles = GetAvailableSubCircleTeamPlayer();
        var teamAAssigned = AssignParticipantToSubCircle(Team.Monster, availableCreatureSubcircles, startingCreatureActor, startingCreatureObject);
        var teamBAssigned = AssignParticipantToSubCircle(Team.Player, availablePlayerSubcircles, startingPlayerActor, startingPlayerObject);

        if (!teamAAssigned || !teamBAssigned) {
            throw new Exception("Failed to assign participants to sub circles.");
        }

        Duel.m_firstTeamToAct = DetermineFirstTeam();

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DuelStartedGracePeriodInSeconds);
        Timers.StartSingleTimer("graceover", new COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER))]
    private void ReceiveGracePeriodOver(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER message) {
        // The grace period for adding participants is now over.
        Logger.Debug("Duel {0} has started.", Logger.Args(Duel.m_duelID));

        EnactActionOnSubCircles(circle => {
            var participantData = circle.CombatParticipant;
            var serializedData = SerializeCombatParticipant(participantData);
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATADD {
                DuelID = SigilId,
                ParticipantData = serializedData,
            };
            DuelBroadcast(msg);
        });

        // Imlight moves too fast for the client. Give some time to the client to catch up.
        var delay = TimeSpan.FromMilliseconds(DelayAfterCombatAddInMs);
        Timers.StartSingleTimer("newround", new COMBAT_106_PROTOCOL.MSG_NEWROUND(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        // Pre-planning phase just wants to send who is up first.
        Duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;
        SendCombatPhase((byte) Duel.m_duelPhase);

        // Inform the combat participants that they may or may not be considered AFK.
        EnactActionOnSubCircles(circle => {
            var participantActor = circle.ParticipantActor;
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATAFK {
                DuelID = SigilId,
                IsCombatAFK = 0
            };
            participantActor.Tell(msg);
        });

        SendUpFirst(Duel.m_roundNum);

        // Planning phase is when each participant notices their new stats and "plans" accordingly.
        // Sending the "planning" phase will cause the client to finally enact the combat cinematic camera.
        Duel.m_duelPhase = kDuelPhase.kPhase_Planning;
        SendCombatPhase((byte) Duel.m_duelPhase);

        SendCombatStats();
        SendCombatHand();
        SendCombatPips();
        SendCombatHealth();

        SendCombatUI(PlanningTime);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ADDPARTICIPANT))]
    private void ReceiveAddParticipant(COMBAT_106_PROTOCOL.MSG_ADDPARTICIPANT message) {
        // We can determine if this participant is a player by checking their template ID. If it's
        // 1, then it's a player. Otherwise, it's a creature.
        var isPlayer = message.ParticipantObject.m_templateID == 1;

        // Check to see if the participant actor is already in the duel. If so, we don't need to do anything.
        var isAlreadyInDuel = _subCircles.Any(x => x.ParticipantActor == message.Participant);
        if (isAlreadyInDuel) {
            return;
        }

        var subCircle = isPlayer ? GetAvailableSubCircleTeamPlayer() : GetAvailableSubCircleTeamCreature();
        var team = isPlayer ? Team.Monster : Team.Player;

        if (subCircle is null || !AssignParticipantToSubCircle(team, subCircle, message.Participant, message.ParticipantObject)) {
            var debugMessage = "Player attempted to join duel {0}, but there were no available sub circles. " +
                                "This should never happen. Send {1} to the duel actor first to check if there are slots available.";
            Logger.Debug(debugMessage, Logger.Args(Duel.m_duelID, nameof(COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE)));
            return;
        }
        else {
            Logger.Debug("Participant joined duel {0}.", Logger.Args(Duel.m_duelID));
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_DUELDETAILS))]
    private void ReceiveDuelDetails(COMBAT_106_PROTOCOL.MSG_DUELDETAILS message) {
        // Received by the DuelActorSupervisor when something is scouting for a duel.
        var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
            DuelActor = Self,
            Duel = Duel,
            CreatureCount = _creatureCount,
            PlayerCount = _playerCount,
        };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE))]
    private void ReceiveSlotAvailable(COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE message) {
        // An actor is checking to see if there are slots available in the duel.
        // Players can only join if there are less than 4 players in the duel.
        // There are 2 creatures per player in the duel. There can only be 4 creatures in the duel.
        var slotAvailable = (message.Team == Team.Player)
            ? PlayerCount   < 4
            : CreatureCount < (PlayerCount * 2) && CreatureCount < 4;
        var rsp = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLERSP { Available = slotAvailable };
        Sender.Tell(rsp);
    }

    private static int DetermineFirstTeam() {
        // Flip a coin to determine which team goes first
        var random = new Random();
        return (int) (random.Next(0, 2) == 0 ? Team.Player : Team.Monster);
    }

    private void SendCombatPhase(byte phase) {
        var upFirstSigilSlot = (byte) (Duel.m_firstTeamToAct == (int) Team.Player ? 4 : 0);

        var serializer = new ObjectSerializer()
                .OnBehaviors(SerializerOptions.Behaviors.None)
                .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                              | SerializerOptions.PropertyFlags.Transmit
                              | SerializerOptions.PropertyFlags.AuthorityTransmit);
        var upFirstData = serializer.Serialize(new UpFirstData() {
            /*
            Record from client. Keeping it here because it's probably important.

            m_resultType = 1884669703,
            m_roundNum = 96,
            m_upFirst = 320,
            */

            m_resultType = 0,
            m_roundNum = Duel.m_roundNum,
            m_upFirst = upFirstSigilSlot,
        });

        var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPHASE() {
            DuelID = SigilId,
            NewPhase = phase,
            PlayerID = 0, // Always recorded as 0
            // todo: unsure why, but client fails to deserialize this
            Data = phase == 1 ? upFirstData : "",
        };

        DuelBroadcast(msg);
    }

    private void SendUpFirst(int roundNum) {
        // This serialized data is used for nearby players to see the combat phase.
        // Unsure the wording, but it sounds like it's used for spectators.
        var upFirstSigilSlot = (byte) (Duel.m_firstTeamToAct == (int) Team.Player ? 4 : 0);

        var upFirstMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATUPFIRST {
            DuelID = SigilId,
            RoundNum = (ushort) roundNum,
            FirstTeamToAct = (byte) Duel.m_firstTeamToAct,
            UpFirst = upFirstSigilSlot,
        };
        DuelBroadcast(upFirstMsg);
    }

    private void SendCombatUI(byte planningPhaseTimer) {
        var combatUiMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SHOWCOMBATUI {
            DuelID = SigilId
        };
        DuelBroadcast(combatUiMsg);

        var planningMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SETPLANNINGPHASETIMER {
            DuelID = SigilId,
            Time = planningPhaseTimer,
        };
        DuelBroadcast(planningMsg);
    }

    private void SendCombatStats() {
        EnactActionOnSubCircles(circle => {
            var participantStats = circle.ParticipantGameStats;
            var serializedStats = SerializeCombatParticipantStat(participantStats);
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATSTATS {
                DuelID = SigilId,
                PartID = circle.ParticipantObject.m_globalID,
                StatsData = serializedStats,
            };
            DuelBroadcast(msg);
        });
    }

    private void SendCombatHand() {
        _serializer.OnPropertyMask(_combatParticipantHandFlags);

        // Serialize the combat hand and send it to the participant, locally.
        // We're skipping creatures for now.
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == Team.Monster) {
                return;
            }
;
            var newHand = circle.DrawHand();
            var buffer = _serializer.Serialize(newHand);

            var participantActor = circle.ParticipantActor;
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATHAND {
                DeckCount = (byte) circle.AvailableSpells,
                TotalDeckCount = (ushort) circle.TotalSpells,
                TreasureCardCount = 0,
                ParticipantID = circle.ParticipantObject.m_globalID,
                HandData = buffer,
            };

            participantActor.Tell(msg);
        });
    }

    private void SendCombatPips() {
        // Create the pip list object.
        var pips = new CombatPipListObj {
            m_duelID = SigilId,
            m_pipList = new List<ParticipantPipData>()
        };

        // Iterate through each sub circle and add the participant's pips to the list.
        EnactActionOnSubCircles(circle => {
            var participantPipData = new ParticipantPipData {
                m_acq = 1,
                m_arch = (uint) MagicSchool.Fire,
                m_archPoints = 0,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
                m_pips = new PipCount() {
                    m_genericPips = 1,
                    m_powerPips = 1,
                }
            };
            pips.m_pipList.Add(participantPipData);

            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPIPS {
                DuelID = SigilId,
            };
        });

        // Serialize the combat pips and send it to each participant.
        _serializer.OnPropertyMask(_combatParticipantStatFlags);
        var buffer = _serializer.Serialize(pips);

        EnactActionOnSubCircles(circle => {
            var participantActor = circle.ParticipantActor;
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPIPS {
                DuelID = SigilId,
                PipData = buffer,
            };
            participantActor.Tell(msg);
        });
    }

    private void SendCombatHealth() {
        // Create the new health list object.
        var healthList = new CombatHealthListObj {
            m_duelID = SigilId,
            m_healthList = new List<ParticipantParameter>()
        };

        // Iterate through each sub circle and add the participant's health to the list.
        EnactActionOnSubCircles(circle => {
            var participantHealth = new ParticipantParameter {
                m_data = 1000,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
            };
            healthList.m_healthList.Add(participantHealth);
        });

        // Serialize the combat health and send it to each participant.
        _serializer.OnPropertyMask(_combatParticipantStatFlags);
        var buffer = _serializer.Serialize(healthList);

        var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATHEALTH {
            DuelID = SigilId,
            HealthData = buffer,
        };
        DuelBroadcast(msg);
    }

    private void EnactActionOnSubCircles(Action<DuelActorSubCircle> action) {
        foreach (var subCircle in ActiveSubCircles) {
            action(subCircle);
        }
    }

    private Duel CreateDuelWithDefaults(ulong sigilId) {
        // todo: source planning time from config
        var duel = new Duel() {
            m_duelID = sigilId,
            m_planningTimer = PlanningTime,
            m_executionPhaseTimer = 3.4078238f,
            m_roundNum = 1,
            m_scalarDamage = _combatSigilTemplate.m_scalarDamagePvE,
            m_scalarResist = _combatSigilTemplate.m_scalarResistPvE,
            m_scalarPierce = _combatSigilTemplate.m_scalarPiercePvE,
            m_damageLimit = _combatSigilTemplate.m_damageLimitPvE,
            m_dK0 = _combatSigilTemplate.m_dK0PvE,
            m_dN0 = _combatSigilTemplate.m_dN0PvE,
            m_resistLimit = _combatSigilTemplate.m_resistLimitPvE,
            m_rK0 = _combatSigilTemplate.m_rK0PvE,
            m_rN0 = _combatSigilTemplate.m_rN0PvE,
        };

        return duel;
    }

    private DuelActorSubCircle[] CreateDuelActorSubCircles(CombatSigilTemplate template) {
        var subCircles = template.m_subCircles;
        var subCircleObjs = new DuelActorSubCircle[8];

        // The sigil rotation is stored between -pi and pi. We need it to be between 0 and 2pi.
        var sigilRotation = _sigilOrientation.Z + MathF.PI;

        for (int i = 0; i < subCircles.Count; i++) {
            var rotation = subCircles[i].m_rotation;
            var radius = subCircles[i].m_radius;
            var color = subCircles[i].m_color;
            var rotationRadians = rotation * (MathF.PI / 180f);

            // The sigil is rotated by some degree. We need to find our x and y coordinates based on this rotation.
            var rotatedX = DuelRadius * MathF.Cos(rotationRadians - sigilRotation);
            var rotatedY = DuelRadius * MathF.Sin(rotationRadians - sigilRotation);
            var x = _sigilLocation.X + rotatedX;
            var y = _sigilLocation.Y + rotatedY;
            var rotatedSigilPos = new Vector3(x, y, _sigilLocation.Z);

            // Now we know where the sigil is located, we need to calculate the facing direction of the sub circle.
            // Calculate the direction vector towards the center of the duel (only Z-axis in radians)
            var duelCenter = new Vector3(_sigilLocation.X - x, _sigilLocation.Y - y, 0);
            var faceTowardsYaw = MathF.Atan2(duelCenter.Y, duelCenter.X);
            // The yaw must be between 0 and 2PI. It must also be reversed as the client rotates clockwise.
            // The translation isn't perfect because of Gamebyro engine bullshit. We need to compensate for this.
            faceTowardsYaw = (2 * MathF.PI) - faceTowardsYaw - YawErrorCompensation;
            if (faceTowardsYaw < 0) {
                faceTowardsYaw += 2 * MathF.PI;
            }

            // Cretae the sub circle object and add it to the array.
            var subCircle = new DuelActorSubCircle(this, radius, faceTowardsYaw, color) {
                WorldPosition = rotatedSigilPos,
                WorldRotation = faceTowardsYaw,
                SlotName = subCircles[i].m_locationPreference,
                SlotType = subCircles[i].m_locationType == "MonsterCircle" ? SlotType.Monster : SlotType.Player
            };
            subCircleObjs[i] = subCircle;
        }

        return subCircleObjs;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamCreature() {
        for (int i = 0; i < 4; i++) {
            if (!_subCircles[i].Occupied) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamPlayer() {
        for (int i = 4; i < 8; i++) {
            if (!_subCircles[i].Occupied) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private bool AssignParticipantToSubCircle(Team team, DuelActorSubCircle subCircle, IActorRef actorRef, CoreObject coreObject) {
        if (subCircle.ParticipantActor != null) {
            return false;
        }

        if (team == Team.Monster) {
            _creatureCount++;
        }
        else {
            _playerCount++;
        }

        subCircle.AssignParticipant(actorRef, coreObject);

        return true;
    }

    private ByteString SerializeCombatParticipant(CombatParticipant participant) {
        _serializer.OnPropertyMask(_combatParticipantFlags);
        var buffer = _serializer.Serialize(participant);

        return buffer;
    }

    private ByteString SerializeCombatParticipantStat(WizGameStats participantStat) {
        _serializer.OnPropertyMask(_combatParticipantStatFlags);
        var buffer = _serializer.Serialize(participantStat);

        return buffer;
    }
}
