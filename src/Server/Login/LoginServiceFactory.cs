/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Login.Services;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Services;

namespace Imlight.Server.Login;

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