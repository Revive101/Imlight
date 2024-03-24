/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Login.Services;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Services;

namespace Imlight.CoreLib.Login;

public class LoginServiceFactory : ServiceFactory {
    protected override HashSet<Type> ServiceTypes { get; set; } = new HashSet<Type>()
    {
        typeof(ControlService),
        typeof(AccountService),
        typeof(AuthenticatorService),
        typeof(CharacterService),
        typeof(GameTransitionService),
        typeof(LoginAFKService),
    };

    public static Props Props() {
        return Akka.Actor.Props.Create(() => new LoginServiceFactory());
    }
}
