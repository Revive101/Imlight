using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;
using Imlight.Net;
using Imlight.Common;
using Akka.Actor;
using Imlight.Net.Services;
using Imlight.Game.Services;

namespace Imlight.Game
{
    public class GameServiceFactory : ServiceFactory
    {
        protected override HashSet<Type> UnloadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(ControlService),
            typeof(AccountService),
        };
        protected override HashSet<Type> LoadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(AttachService),
        };

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new GameServiceFactory());
        }
    }
}
