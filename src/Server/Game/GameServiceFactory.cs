using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Game.Services;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Services;

namespace Imlight.Server.Game
{
    public class GameServiceFactory : ServiceFactory
    {
        protected override HashSet<Type> UnloadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(ControlService),
        };
        protected override HashSet<Type> LoadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(AttachService),
            typeof(AccountService),
            typeof(ClientService),
            typeof(MoveService),
            typeof(ZoneService),
            typeof(CharacterService),
            typeof(ChatService),
            typeof(SpellService),
            typeof(InventoryService)
        };

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new GameServiceFactory());
        }
    }
}
