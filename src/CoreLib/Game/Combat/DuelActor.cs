using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Represents a duel. This is the actor that manages the duel. It is created by the
/// <see cref="DuelActorSupervisor"/> and is a child of it.
/// </summary>
public class DuelActor : ReceiveProtocolDispatcher, IWithTimers {
    private const float DuelRadius = 584.0f;
    private const float AngleBetweenSubCircles = 15.0f;
    private const float FirstSubCircleAngle = 52.0f;
    private const float OrientationCompactionFactor = 0.708f;
    private const float DuelStartedGracePeriodInSeconds = 3.0f;

    public ITimerScheduler Timers { get; set; }

    private Duel _duel;
    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];

    private Dictionary<IActorRef, CoreObject> _participants;
    private ulong _sigilId; // Same as the sigil ID.
    private Vector3 _sigilLocation;
    private Vector3 _sigilOrientation;

    public DuelActor() {
        _duel = new Duel();
        _participants = new Dictionary<IActorRef, CoreObject>();
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new DuelActor());

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
        this._participants = message.Participants;
        this._sigilId = message.SigilId;
        this._sigilLocation = message.SigilLocation;
        this._sigilOrientation = message.SigilOrientation;

        _duel = CreateDuelWithDefaults(message.SigilId);
        _subCircles = CreateDuelActorSubCircles();

        // Return the duel details to the sender. As it stands, this is returned to
        // the WizardZoneCombatSigil that created this duel actor.
        var rsp = new COMBAT_106_PROTOCOL.MSG_DUELDETAILS {
            DuelActor = Self,
            Duel = _duel
        };
        Sender.Tell(rsp);

        // At this point, there are only two participants. One from each team.
        // Assign them to the sub circles. Team A will always be the player and
        // team B will always be the creature.
        var teamA = GetAvailableSubCircleTeamA();
        var teamB = GetAvailableSubCircleTeamB();
        var teamAAssigned = AssignParticipantToSubCircle(teamA, message.Participants.Keys.First(), message.Participants.Values.First());
        var teamBAssigned = AssignParticipantToSubCircle(teamB, message.Participants.Keys.Last(), message.Participants.Values.Last());

        if (!teamAAssigned || !teamBAssigned) {
            throw new Exception("Failed to assign participants to sub circles.");
        }

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DuelStartedGracePeriodInSeconds);
        Timers.StartSingleTimer("graceover", new COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER))]
    private void ReceiveGracePeriodOver(COMBAT_106_PROTOCOL.MSG_GRACEPERIODOVER message) {
        // The grace period for adding participants is now over.
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ADDPARTICIPANT))]
    private void ReceiveAddParticipant(COMBAT_106_PROTOCOL.MSG_ADDPARTICIPANT message) {
        // In a duel, the players will always be team A and the creatures will always be team B.
        // We can determine if this participant is a player by checking their template ID. If it's
        // 1, then it's a player. Otherwise, it's a creature.
        var isPlayer = message.ParticipantObject.m_templateID == 1;
        var subCircle = isPlayer ? GetAvailableSubCircleTeamA() : GetAvailableSubCircleTeamB();

        if (subCircle is null || !AssignParticipantToSubCircle(subCircle, message.Participant, message.ParticipantObject)) {
            Logger.Debug("Player attempted to join duel {0}, but the duel was full.", Logger.Args(_duel.m_duelID));
        }
    }

    private Duel CreateDuelWithDefaults(ulong sigilId) {
        // todo: source these from config
        var duel = new Duel() {
            m_flatParticipantList = new(),
            m_duelID = sigilId,
            m_planningTimer = 30,
            m_firstTeamToAct = 0,
            m_executionPhaseTimer = 3.4078238f,
            m_duelPhase = Duel.kDuelPhase.kPhase_PrePlanning,
        };

        return duel;
    }

    private DuelActorSubCircle[] CreateDuelActorSubCircles() {
        var subCircles = new DuelActorSubCircle[8];

        // Calculate the initial angle for the first sub circle.
        var initialAngle = MathF.PI * FirstSubCircleAngle / 180; // Degrees --> Radians

        // Calculate the rotated angle based on the sigil's orientation.
        var rotatedAngle = MathF.PI * _sigilOrientation.Z / (180 * OrientationCompactionFactor); // Degrees --> Radians

        for (int i = 0; i < 4; i++) {
            var angle = initialAngle + i * AngleBetweenSubCircles;

            var x = _sigilLocation.X + DuelRadius * MathF.Cos((float) (angle));
            var y = _sigilLocation.Y + DuelRadius * MathF.Sin((float) (angle));

            var direction = new Vector3(x, y, _sigilLocation.Z / OrientationCompactionFactor) - _sigilLocation;
            direction.Normalize();

            subCircles[i] = new DuelActorSubCircle(new Vector3(x, y, _sigilLocation.Z), direction, _sigilId);

            // Mirror for bottom hemisphere
            subCircles[i + 4] = new DuelActorSubCircle(new Vector3(x, -y, _sigilLocation.Z), direction, _sigilId);
        }

        return subCircles;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamA() {
        for (int i = 0; i < 4; i++) {
            if (_subCircles[i].Participant == null) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private DuelActorSubCircle GetAvailableSubCircleTeamB() {
        for (int i = 4; i < 8; i++) {
            if (_subCircles[i].Participant == null) {
                return _subCircles[i];
            }
        }

        return null;
    }

    private bool AssignParticipantToSubCircle(DuelActorSubCircle subCircle, IActorRef actorRef, CoreObject coreObject) {
        if (subCircle.Participant != null) {
            return false;
        }

        subCircle.AssignParticipant(actorRef, coreObject);

        return true;
    }

    private void SendCombatStartToParticipants(Dictionary<IActorRef, CoreObject> participants) {
        foreach (var participant in participants) {
            // A player will always have a template ID of 1.
            CombatParticipant combatParticipant;
            if (participant.Value.m_templateID == 1) {
                combatParticipant = CreateCombatParticipantFromPlayer(participant.Key, participant.Value);
            }
            else {
                combatParticipant = CreateCombatParticipantFromCreature(participant.Value);
            }

            // Serialize and send the combat participant to the participant.
            var serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                          | SerializerOptions.PropertyFlags.Transmit
                          | SerializerOptions.PropertyFlags.AuthorityTransmit);
            var serializedCombatParticipant = serializer.Serialize(combatParticipant);

            // Send to client.
            var addMsg = new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATADD {
                DuelID = _sigilId,
                ParticipantData = serializedCombatParticipant
            };

            participant.Key.Tell(addMsg);
        }
    }

    private CombatParticipant CreateCombatParticipantFromPlayer(IActorRef playerActor, CoreObject playerObject) {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
        var queryCharacterRsp = playerActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Character;

        var combatParticipant = new CombatParticipant {
            m_ownerID = playerObject.m_globalID,
            m_templateID = playerObject.m_templateID, // Captued 2199023255553 from live
            m_isPlayer = true,
            m_zoneID = 0,
            m_teamID = 0,
            m_primaryMagicSchoolID = 0,
            m_pipCount = new() { m_powerPips = 1, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_PipsSuspended = false,
            m_playerHealth = queryCharacterRsp.GameStats.m_currentHitpoints,
            m_maxPlayerHealth = queryCharacterRsp.GameStats.m_baseHitpoints,

            // todo: this causes client to fail deserialization
            //m_pGameStats = queryCharacterRsp.GameStats,
        };

        return combatParticipant;
    }

    private CombatParticipant CreateCombatParticipantFromCreature(CoreObject creatureObject) {
        var combatParticipant = new CombatParticipant {
            m_ownerID = creatureObject.m_globalID,
            m_templateID = creatureObject.m_templateID, // Captued 2199023290637 from live
            m_isPlayer = true,
            m_zoneID = 0,
            m_teamID = 1,
            m_primaryMagicSchoolID = 0,
            m_pipCount = new() { m_powerPips = 1, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_PipsSuspended = false,
        };

        return combatParticipant;

    }
}
