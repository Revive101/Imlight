using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Services;

namespace Imlight.Login
{
    public class LoginServiceActorFactory : ServiceFactory
    {
        public LoginServiceActorFactory()
        {
            ConfigureReceivers();
        }

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new LoginServiceActorFactory());
        }

        protected override void ConfigureReceivers()
        {
            Receive<string>(x => x == UNLOADED_SERVICES_ASK, x => GetUnloadedActorMessageServices());
            Receive<string>(x => x == LOADED_SERVICES_ASK, x => GetLoadedActorMessageServices());
        }

        protected override void GetUnloadedActorMessageServices()
        {
            var set = new HashSet<Type>()
            {
                typeof(ControlServiceActor),
            };

            Sender.Tell(set);
        }

        protected override void GetLoadedActorMessageServices()
        {
            var set = new HashSet<Type>()
            {

            };

            Sender.Tell(set);
        }
    }
}
