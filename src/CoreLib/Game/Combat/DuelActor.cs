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
    private const float DuelRadius = 584.0f;
    private const byte NumOfSubCircles = 8;
    private const byte PlanningTime = 30;
    private const float AngleBetweenSubCircles = 36.8f;
    private const float FirstSubCircleAngle = 145.3f;
    private const float DuelStartedGracePeriodInSeconds = 3.0f;
    private const float YawErrorCompensation = 1.58f;
    private const int DelayAfterCombatAddInMs = 1000;

    public ITimerScheduler Timers { get; set; }

    private readonly IActorRef _wizardZoneRef;
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
        .OnBehaviors(SerializerOptions.Behaviors.None);
    private readonly SerializerOptions.PropertyFlags _combatParticipantFlags = (SerializerOptions.PropertyFlags) 4;
    private readonly SerializerOptions.PropertyFlags _combatParticipantStatFlags = (SerializerOptions.PropertyFlags) 5;
    private readonly SerializerOptions.PropertyFlags _combatParticipantHandFlags = (SerializerOptions.PropertyFlags) 5;

    private byte PlayerCount => (byte) _subCircles.Count(x => x.IsOccupied && x.Team == Team.Player);
    private byte CreatureCount => (byte) _subCircles.Count(x => x.IsOccupied && x.Team == Team.Creature);
    private DuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.IsOccupied).ToArray();

    private Duel _duel;
    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];
    private ulong _sigilId;
    private Vector3 _sigilLocation;
    private Vector3 _sigilOrientation;
    private byte _creatureCount;
    private byte _playerCount;
    private Team _upFirstTeam;

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
        this._sigilId = message.SigilId;
        this._sigilLocation = message.SigilLocation;
        this._sigilOrientation = message.SigilOrientation;

        // Create duel object and the 8 subcircles.
        _duel = CreateDuelWithDefaults(message.SigilId);
        _subCircles = CreateDuelActorSubCircles();

        // Return the duel details to the sender. This is sent back to the sigil that requested the duel.
        var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
            DuelActor = Self,
            Duel = _duel
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
        var teamAAssigned = AssignParticipantToSubCircle(Team.Creature, availableCreatureSubcircles, startingCreatureActor, startingCreatureObject);
        var teamBAssigned = AssignParticipantToSubCircle(Team.Player, availablePlayerSubcircles, startingPlayerActor, startingPlayerObject);

        if (!teamAAssigned || !teamBAssigned) {
            throw new Exception("Failed to assign participants to sub circles.");
        }

        _upFirstTeam = DetermineFirstTeam();

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DuelStartedGracePeriodInSeconds);
        Timers.StartSingleTimer("graceover", new COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER))]
    private void ReceiveGracePeriodOver(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER message) {
        // The grace period for adding participants is now over.
        Logger.Debug("Duel {0} has started.", Logger.Args(_duel.m_duelID));

        _duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;

        EnactActionOnSubCircles(circle => {
            var participantData = circle.GetParticipant();
            var serializedData = SerializeCombatParticipant(participantData);
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATADD {
                DuelID = _sigilId,
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
        _duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;
        SendCombatPhase((byte) _duel.m_duelPhase);
        SendUpFirst(_duel.m_roundNum);

        // Planning phase is when each participant notices their new stats and "plans" accordingly.
        _duel.m_duelPhase = kDuelPhase.kPhase_Planning;
        SendCombatPhase((byte) _duel.m_duelPhase);
        SendCombatStats();
        //SendCombatHand();
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
        var team = isPlayer ? Team.Creature : Team.Player;

        if (subCircle is null || !AssignParticipantToSubCircle(team, subCircle, message.Participant, message.ParticipantObject)) {
            var debugMessage = "Player attempted to join duel {0}, but there were no available sub circles. " +
                                "This should never happen. Send {1} to the duel actor first to check if there are slots available.";
            Logger.Debug(debugMessage, Logger.Args(_duel.m_duelID, nameof(COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLE)));
            return;
        }
        else {
            Logger.Debug("Participant joined duel {0}.", Logger.Args(_duel.m_duelID));
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_DUELDETAILS))]
    private void ReceiveDuelDetails(COMBAT_106_PROTOCOL.MSG_DUELDETAILS message) {
        // Received by the DuelActorSupervisor when something is scouting for a duel.
        var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
            DuelActor = Self,
            Duel = _duel,
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

    private static Team DetermineFirstTeam() {
        // Flip a coin to determine which team goes first
        var random = new Random();
        return random.Next(0, 2) == 0 ? Team.Player : Team.Creature;
    }

    private void SendCombatPhase(byte phase) {
        var upFirstSigilSlot = (byte) (_upFirstTeam == Team.Player ? 4 : 0);

        // Don't remove this serializer. I don't know why the class serializer fails for this, but it does.
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
            m_roundNum = _duel.m_roundNum,
            m_upFirst = upFirstSigilSlot,
        });

        var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPHASE() {
            DuelID = _sigilId,
            NewPhase = phase,
            PlayerID = 0, // Always recorded as 0
            // todo: unsure why, but client fails to deserialize this
            //Data = phase == 1 ? upFirstData : "",
        };

        DuelBroadcast(msg);
    }

    private void SendUpFirst(int roundNum) {
        var upFirstSigilSlot = (byte) (_upFirstTeam == Team.Player ? 4 : 0);

        var upFirstMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATUPFIRST {
            DuelID = _sigilId,
            RoundNum = (ushort) roundNum,
            FirstTeamToAct = (byte) (_upFirstTeam == Team.Player ? 1 : 0),
            UpFirst = upFirstSigilSlot,
        };
        DuelBroadcast(upFirstMsg);
    }

    private void SendCombatUI(byte planningPhaseTimer) {
        var combatUiMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SHOWCOMBATUI {
            DuelID = _sigilId
        };
        DuelBroadcast(combatUiMsg);

        var planningMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_SETPLANNINGPHASETIMER {
            DuelID = _sigilId,
            Time = planningPhaseTimer,
        };
        DuelBroadcast(planningMsg);
    }

    private void SendCombatStats() {
        EnactActionOnSubCircles(circle => {
            var participantStats = circle.ParticipantGameStats;
            var serializedStats = SerializeCombatParticipantStat(participantStats);
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATSTATS {
                DuelID = _sigilId,
                PartID = circle.ParticipantObject.m_globalID,
                StatsData = serializedStats,
            };
            DuelBroadcast(msg);
        });
    }

    private void SendCombatHand() {
        var spellTest = new Spell() {
            m_templateID = 100422,
            m_pipCost = new SpellRank() {
                m_spellRank = 1,
                m_firePips = 1,
            }
        };
        var spellTest2 = new Spell() {
            m_templateID = 1552060,
            m_pipCost = new SpellRank() {
                m_spellRank = 1,
                m_firePips = 1,
            }
        };
        var hand = new Hand {
            m_spellList = new List<Spell> ()
        };

        _serializer.OnPropertyMask(_combatParticipantHandFlags);
        var buffer = _serializer.Serialize(hand);

        // Serialize the combat hand and send it to the participant, locally.
        // Skip the creatures, as they don't have a hand.
        EnactActionOnSubCircles(circle => {
            var participantActor = circle.ParticipantActor;
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATHAND {
                DeckCount = 7,
                TotalDeckCount = 7,
                ParticipantID = circle.ParticipantObject.m_globalID,
                HandData = buffer,
            };

            participantActor.Tell(msg);
        });
    }

    private void SendCombatPips() {
        // Create the pip list object.
        var pips = new CombatPipListObj {
            m_duelID = _sigilId,
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
                    m_genericPips = 1
                }
            };
            pips.m_pipList.Add(participantPipData);

            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPIPS {
                DuelID = _sigilId,
            };
        });

        // Serialize the combat pips and send it to each participant.
        _serializer.OnPropertyMask(_combatParticipantStatFlags);
        var buffer = _serializer.Serialize(pips);

        EnactActionOnSubCircles(circle => {
            var participantActor = circle.ParticipantActor;
            var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATPIPS {
                DuelID = _sigilId,
                PipData = buffer,
            };
            participantActor.Tell(msg);
        });
    }

    private void SendCombatHealth() {
        // Create the new health list object.
        var healthList = new CombatHealthListObj {
            m_duelID = _sigilId,
            m_healthList = new List<ParticipantParameter>()
        };

        // Iterate through each sub circle and add the participant's health to the list.
        EnactActionOnSubCircles(circle => {
            var participantHealth = new ParticipantParameter {
                m_data = 55, // todo: change
                m_partID = (GID) circle.ParticipantObject.m_globalID,
            };
            healthList.m_healthList.Add(participantHealth);
        });

        // Serialize the combat health and send it to each participant.
        _serializer.OnPropertyMask(_combatParticipantStatFlags);
        var buffer = _serializer.Serialize(healthList);

        var msg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATHEALTH {
            DuelID = _sigilId,
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
        // todo: source these from config
        var duel = new Duel() {
            m_flatParticipantList = new(),
            m_duelID = sigilId,
            m_planningTimer = PlanningTime,
            m_firstTeamToAct = 0,
            m_executionPhaseTimer = 3.4078238f,
            m_duelPhase = kDuelPhase.kPhase_Starting,
            m_roundNum = 1,
        };

        return duel;
    }

    private DuelActorSubCircle[] CreateDuelActorSubCircles() {
        var subCircles = new DuelActorSubCircle[8];
        var workingAngle = FirstSubCircleAngle;

        for (int i = 0; i < NumOfSubCircles / 2; i++) {
            var angleRadians = workingAngle * (MathF.PI / 180f);

            // The sigil rotation is stored between -pi and pi. We need to convert it to 0-2PI.
            var sigilRotation = _sigilOrientation.Z;
            if (sigilRotation < 0) {
                sigilRotation = (2 * MathF.PI) + sigilRotation;
            }

            // The sigil is rotated by some degree. We need to find our x and y coordinates based on this rotation.
            var rotatedX = DuelRadius * MathF.Cos(angleRadians - sigilRotation);
            var rotatedY = DuelRadius * MathF.Sin(angleRadians - sigilRotation);
            var x = _sigilLocation.X + rotatedX;
            var y = _sigilLocation.Y + rotatedY;
            var sigilPosition = new Vector3(x, y, _sigilLocation.Z);

            // Now we know where the sigil is located, we need to calculate the yaw for the sub circle.
            // Calculate the direction vector towards the center of the duel (only Z-axis in radians)
            var directionVector = new Vector3(_sigilLocation.X - x, _sigilLocation.Y - y, 0);
            var yaw = MathF.Atan2(directionVector.Y, directionVector.X);
            // The yaw must be between 0 and 2PI. It must also be reversed as the client rotates clockwise.
            // The translation isn't perfect because of Gamebyro engine bullshit. We need to compensate for this.
            yaw = (2 * MathF.PI) - yaw - YawErrorCompensation;
            if (yaw < 0) {
                yaw += 2 * MathF.PI;
            }

            // Create the sub circle and add it to the array of sub circles.
            var subCircleId = (byte)(i + 1);
            subCircles[i] = new DuelActorSubCircle(this, sigilPosition, yaw, _sigilId, subCircleId);

            // Mirror for the bottom hemisphere (mirrored on both X and Y axes)
            var mirroredX = _sigilLocation.X - rotatedX;
            var mirroredY = _sigilLocation.Y - rotatedY;
            var mirroredPos = new Vector3(mirroredX, mirroredY, _sigilLocation.Z);
            var mirroredYaw = yaw + MathF.PI;
            mirroredYaw = (mirroredYaw < 0) ? (2 * MathF.PI) + mirroredYaw : mirroredYaw; // Normalize to 0-2PI

            subCircleId = (byte)(i + 4);
            subCircles[i + 4] = new DuelActorSubCircle(this, mirroredPos, mirroredYaw, _sigilId, subCircleId);

            // Update initial angle for the next sub-circle
            workingAngle -= AngleBetweenSubCircles;
        }

        return subCircles;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamCreature() {
        for (int i = 0; i < 4; i++) {
            if (!_subCircles[i].IsOccupied) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamPlayer() {
        for (int i = 4; i < 8; i++) {
            if (!_subCircles[i].IsOccupied) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private bool AssignParticipantToSubCircle(Team team, DuelActorSubCircle subCircle, IActorRef actorRef, CoreObject coreObject) {
        if (subCircle.ParticipantActor != null) {
            return false;
        }

        if (team == Team.Creature) {
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
