using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Login.Models;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandContext {
    public IActorRef SessionActor { get; init; }
    public TypeCache.CoreObject CharacterObject { get; init; }
    public Character Character { get; init; }
    public Account Account { get; init; }
    public IActorRef ZoneActor { get; init; }
    public IActorRef ServerActor { get; init; }
}
