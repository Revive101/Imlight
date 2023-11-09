using Akka.Actor;
using Imlight.Common.Caches;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

public class DuelActorSubCircle {
    public Vector3 Location { get; set; }
    public Vector3 Orientation { get; set; }
    public CombatParticipant Participant { get; set; }
    public IActorRef Actor { get; set; }
    public CoreObject ParticipantObject { get; set; }
}
