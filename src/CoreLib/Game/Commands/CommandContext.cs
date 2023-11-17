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

    public CommandContext(IActorRef sessionActor, TypeCache.CoreObject characterObject, Character character, Account account) {
        SessionActor = sessionActor;
        CharacterObject = characterObject;
        Character = character;
        Account = account;
    }
}
