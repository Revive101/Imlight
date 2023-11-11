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
    private const byte NumOfSubCircles = 8;
    private const float AngleBetweenSubCircles = 36.8f;
    private const float FirstSubCircleAngle = 145.0f;
    private const float DuelStartedGracePeriodInSeconds = 3.0f;
    private const float YawErrorCompensation = 1.58f;

    public ITimerScheduler Timers { get; set; }
    private byte PlayerCount => (byte) _subCircles.Count(x => x.IsOccupied && x.Team == Team.Player);
    private byte CreatureCount => (byte) _subCircles.Count(x => x.IsOccupied && x.Team == Team.Creature);

    private Duel _duel;
    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];
    private ulong _sigilId;
    private Vector3 _sigilLocation;
    private Vector3 _sigilOrientation;
    private byte _creatureCount;
    private byte _playerCount;

    public DuelActor() { }

    public static Props Props() => Akka.Actor.Props.Create(() => new DuelActor());

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_STARTDUEL))]
    private void ReceiveStartDuel(COMBAT_106_PROTOCOL.MSG_STARTDUEL message) {
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
        // Assign them to the sub circles. Team A will always be the creatures and
        // team B will always be the players.
        var teamCreatures = GetAvailableSubCircleTeamCreature();
        var teamPlayers = GetAvailableSubCircleTeamPlayer();
        var teamAAssigned = AssignParticipantToSubCircle(Team.Creature, teamCreatures, message.Participants.Keys.Last(), message.Participants.Values.Last());
        var teamBAssigned = AssignParticipantToSubCircle(Team.Player, teamPlayers, message.Participants.Keys.First(), message.Participants.Values.First());

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

        // Check to see if the participant actor is already in the duel. If so, we don't need to do anything.
        var isAlreadyInDuel = _subCircles.Any(x => x.Participant == message.Participant);
        if (isAlreadyInDuel) {
            return;
        }

        var subCircle = isPlayer ? GetAvailableSubCircleTeamPlayer() : GetAvailableSubCircleTeamCreature();
        var team = isPlayer ? Team.Creature : Team.Player;

        if (subCircle is null || !AssignParticipantToSubCircle(team, subCircle, message.Participant, message.ParticipantObject)) {
            Logger.Debug("Player attempted to join duel {0}, but the duel was full.", Logger.Args(_duel.m_duelID));
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_DUELDETAILS))]
    private void ReceiveDuelDetails(COMBAT_106_PROTOCOL.MSG_DUELDETAILS message) {
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
        if (message.Team == Team.Player) {
            var available = PlayerCount < 4;
            var rsp = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLERSP {
                Available = available
            };
            Sender.Tell(rsp);
        }
        else {
            // For every player, there are 2 creatures allowed to a max of 4 creatures.
            var available = CreatureCount < (PlayerCount * 2) && CreatureCount < 4;
            var rsp = new COMBAT_106_PROTOCOL.MSG_SLOTAVAILABLERSP {
                Available = available
            };
            Sender.Tell(rsp);
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
        var workingAngle = FirstSubCircleAngle;

        for (int i = 0; i < NumOfSubCircles / 2; i++) {
            var angleRadians = workingAngle * (MathF.PI / 180f);

            // The sigil rotation is stored between -pi and pi. We need to convert it to 0-2PI.
            var sigilRotation = _sigilOrientation.Z;
            if (sigilRotation < 0) {
                sigilRotation = (2 * MathF.PI) + sigilRotation;
            }

            // Calculate rotated position
            var rotatedX = DuelRadius * MathF.Cos(angleRadians - sigilRotation);
            var rotatedY = DuelRadius * MathF.Sin(angleRadians - sigilRotation);
            var x = _sigilLocation.X + rotatedX;
            var y = _sigilLocation.Y + rotatedY;
            var sigilPosition = new Vector3(x, y, _sigilLocation.Z);

            // Calculate the direction vector towards the center of the duel (only Z-axis in radians)
            var directionVector = new Vector3(_sigilLocation.X - x, _sigilLocation.Y - y, 0);
            var yaw = MathF.Atan2(directionVector.Y, directionVector.X);
            // The yaw must be between 0 and 2PI. It must also be reversed as the client rotates clockwise.
            yaw = (2 * MathF.PI) - yaw - YawErrorCompensation;
            if (yaw < 0) {
                yaw += 2 * MathF.PI;
            }

            subCircles[i] = new DuelActorSubCircle(sigilPosition, yaw, _sigilId);

            // Mirror for the bottom hemisphere (mirrored on both X and Y axes)
            var mirroredX = _sigilLocation.X - rotatedX;
            var mirroredY = _sigilLocation.Y - rotatedY;
            var mirroredPos = new Vector3(mirroredX, mirroredY, _sigilLocation.Z);
            var mirroredYaw = yaw + MathF.PI;
            mirroredYaw = (mirroredYaw < 0) ? (2 * MathF.PI) + mirroredYaw : mirroredYaw; // Normalize to 0-2PI

            subCircles[i + 4] = new DuelActorSubCircle(mirroredPos, mirroredYaw, _sigilId);

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
        if (subCircle.Participant != null) {
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
