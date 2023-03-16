using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Services;
using Imlight.Login.Services;

namespace Imlight.Login
{
    public class LoginServiceFactory : ServiceFactory
    {
        protected override HashSet<Type> UnloadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(ControlService),
            typeof(AccountService),
        };
        protected override HashSet<Type> LoadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(AuthenticatorService),
            typeof(CharacterService),
            typeof(GameTransitionService),
            typeof(LoginAFKService),
        };

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new LoginServiceFactory());
        }
    }
}
