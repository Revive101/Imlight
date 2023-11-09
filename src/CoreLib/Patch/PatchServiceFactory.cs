/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Patch.Services;
using Imlight.CoreLib.Shared.Services;

namespace Imlight.CoreLib.Patch;

public class PatchServiceFactory : ServiceFactory {
    protected override HashSet<Type> UnloadedServiceTypes { get; set; } = new HashSet<Type>()
    {
        typeof(ControlService),
        typeof(PatchService)
    };
    protected override HashSet<Type> LoadedServiceTypes { get; set; } = new HashSet<Type>() {
    };

    public static Props Props() {
        return Akka.Actor.Props.Create(() => new PatchServiceFactory());
    }
}
