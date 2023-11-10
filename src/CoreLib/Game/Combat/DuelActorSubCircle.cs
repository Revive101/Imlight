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
    public Vector3 Orientation { get; set; }
    public CombatParticipant Participant { get; set; }
    public IActorRef Actor { get; set; }
    public CoreObject ParticipantObject { get; set; }

    private ulong _sigilId;

    public DuelActorSubCircle(Vector3 location, Vector3 orientation, ulong sigilId) {
        Location = location;
        Orientation = orientation;
        _sigilId = sigilId;
    }

    public async Task AssignParticipant(IActorRef actor, CoreObject participantObject) {
        Actor = actor;
        ParticipantObject = participantObject;

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
            Yaw = Orientation.Z,
            SigilGID = _sigilId
        };
        actor.Tell(aggroMsg);

        // Wait the amount of time it takes for the actor to enter the sigil, then set
        // their state to combat idle.
        await Task.Delay((int) (AggroTimeInSeconds * 1000));

        // Set state.
        ((GAME_5_PROTOCOL.MSG_ENTERSTATE)broadcastMsg.Message).State = (uint) State.Unknown_2;
        actor.Tell(broadcastMsg);
    }
}
