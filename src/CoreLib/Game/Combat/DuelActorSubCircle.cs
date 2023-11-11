using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

public class DuelActorSubCircle {
    private const float AggroTimeInSeconds = 0.75f;

    public Vector3 Location { get; set; }
    public float Yaw { get; set; }
    public CombatParticipant Participant { get; set; }
    public IActorRef Actor { get; set; }
    public CoreObject ParticipantObject { get; set; }
    public bool IsOccupied { get; set; }
    public Team Team { get; set; }

    private ulong _sigilId;

    public DuelActorSubCircle(Vector3 location, float yaw, ulong sigilId) {
        Location = location;
        Yaw = yaw;
        _sigilId = sigilId;
    }

    public async Task AssignParticipant(IActorRef actor, CoreObject participantObject) {
        Actor = actor;
        ParticipantObject = participantObject;
        Team = participantObject.m_templateID == 1 ? Team.Player : Team.Creature;
        IsOccupied = true;

        await PlayEntranceAnimation(actor, participantObject);
    }

    private async Task PlayEntranceAnimation(IActorRef actor, CoreObject participantObject) {
        // Set the state of the participant to entering sigil.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST() {
            Selfless = false,
            Sender = actor,
            Message = new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
                GameObjectID = participantObject.m_globalID,
                State = (uint) State.Sigil
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

        // Set state.
        ((GAME_5_PROTOCOL.MSG_ENTERSTATE)broadcastMsg.Message).State = (uint) State.CombatIdle;
        actor.Tell(broadcastMsg);
    }
}
