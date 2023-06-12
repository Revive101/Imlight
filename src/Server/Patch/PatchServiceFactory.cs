using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Patch.Services;
using Imlight.Server.Shared.Services;

namespace Imlight.Server.Patch
{
    public class PatchServiceFactory : ServiceFactory
    {
        protected override HashSet<Type> UnloadedServiceTypes { get; set; } = new HashSet<Type>()
        {
            typeof(ControlService),
            typeof(PatchService)
        };
        protected override HashSet<Type> LoadedServiceTypes { get; set; } = new HashSet<Type>()
        {
        };

        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new PatchServiceFactory());
        }
    }
}
