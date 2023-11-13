using System.Drawing;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.TypeCache.CombatParticipant;

namespace Imlight.CoreLib.Game.Combat;

public class DuelActorSubCircle {
    private const float AggroTimeInSeconds = 0.75f;

    public Vector3 Location { get; set; }
    public byte SubCircleId { get; set; }
    public float Yaw { get; set; }
    public CombatParticipant Participant { get; set; }
    public IActorRef Actor { get; set; }
    public CoreObject ParticipantObject { get; set; }
    public bool IsOccupied { get; set; }
    public Team Team { get; set; }

    private readonly ulong _sigilId;

    public DuelActorSubCircle(Vector3 location, float yaw, ulong sigilId, byte subCircleId) {
        Location = location;
        SubCircleId = subCircleId;
        Yaw = yaw;
        _sigilId = sigilId;
    }

    internal async Task AssignParticipant(IActorRef actor, CoreObject participantObject) {
        Actor = actor;
        ParticipantObject = participantObject;
        Team = participantObject.m_templateID == 1 ? Team.Player : Team.Creature;
        IsOccupied = true;

        await PlayEntranceAnimation(actor, participantObject);
    }

    internal ByteString GetSerializedCombatParticipant() {
        // Get the combat participant and serialize it.
        Participant = GetParticipant();
        var serializer = new ObjectSerializer()
        .OnBehaviors(SerializerOptions.Behaviors.None)
        .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                      | SerializerOptions.PropertyFlags.Transmit
                      | SerializerOptions.PropertyFlags.AuthorityTransmit);

        return serializer.Serialize(Participant);
    }

    private async Task PlayEntranceAnimation(IActorRef actor, CoreObject participantObject) {
        // Set the state of the participant to entering sigil.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST() {
            Selfless = false,
            Sender = actor,
            Message = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
                GameObjectID = participantObject.m_globalID,
                State = (uint) NPCStates.Sigil
            }
        };
        actor.Tell(broadcastMsg);

        // Send aggro to the participant.
        var aggroMsg = new WIZARD_12_PROTOCOL.MSG_AGGRO {
            GlobalID = participantObject.m_globalID,
            LocX = Location.X,
            LocY = Location.Y,
            LocZ = Location.Z,
            Yaw = this.Yaw,
            SigilGID = _sigilId
        };
        broadcastMsg.Message = aggroMsg;
        actor.Tell(broadcastMsg);

        // Wait the amount of time it takes for the actor to enter the sigil, then set
        // their state to combat idle.
        await Task.Delay((int) (AggroTimeInSeconds * 1000));

        // Set state to stationary.
        var secondBroadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST() {
            Selfless = false,
            Sender = actor,
            Message = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
                GameObjectID = participantObject.m_globalID,
                State = (uint) NPCStates.Stationary
            }
        };
        actor.Tell(secondBroadcastMsg);
    }

    private CombatParticipant GetParticipant() {
        if (Team == Team.Player) {
            return GetPlayerParicipant();
        } else {
            return GetCreatureParticipant();
        }
    }

    private CombatParticipant GetPlayerParicipant() {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
        var queryCharacterRsp = Actor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Character;

        // Get DynamicSigilSymbol enum by value using our SubCircleId. Skip values 5-8.
        var dynamicSigilSymbol = (DynamicSigilSymbol) (SubCircleId < 5 ? SubCircleId : SubCircleId + 4);

        var combatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023255553, // Captued 2199023255553 from live
            m_isPlayer = true,
            m_zoneID = 0,
            m_teamID = 0,
            m_primaryMagicSchoolID = 83375795,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_PipsSuspended = false,
            m_originalTeam = 0,
            m_maxHandSize = 7,
            m_playerHealth = queryCharacterRsp.GameStats.m_currentHitpoints,
            m_maxPlayerHealth = queryCharacterRsp.GameStats.m_baseHitpoints,
            m_color = (Color3) SharpDX.Color.Green,
            //m_rotation = Yaw, // This crashes the client if present
            m_subcircle = -256,
            m_dynamicSymbol = DynamicSigilSymbol.NotSet,

            // todo: this causes client to fail deserialization
            //m_pGameStats = queryCharacterRsp.GameStats,
        };

        return combatParticipant;
    }

    private CombatParticipant GetCreatureParticipant() {
        // Get DynamicSigilSymbol enum by value using our SubCircleId. Skip values 5-8.
        var dynamicSigilSymbol = (DynamicSigilSymbol) (SubCircleId < 5 ? SubCircleId : SubCircleId + 4);

        var combatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023290637, // Captured 2199023290637 from live
            m_isPlayer = false,
            m_isMonster = 0,
            m_zoneID = 0,
            m_teamID = 1,
            m_originalTeam = 1,
            m_maxHandSize = 7,
            m_primaryMagicSchoolID = 83375795,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_PipsSuspended = false,
            m_color = (Color3) SharpDX.Color.Red,
            //m_rotation = Yaw, // This crashes the client when present
            m_subcircle = -256,
            m_dynamicSymbol = DynamicSigilSymbol.NotSet,
        };

        return combatParticipant;

    }
}
