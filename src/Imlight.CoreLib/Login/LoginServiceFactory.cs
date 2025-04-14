/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 * 
 * ========================================================================
 * LOGIN SERVICE FACTORY 
 * ========================================================================
 * 
 * PURPOSE:
 * Defines and registers the service types required for login operations,
 * providing a centralized factory for creating login-related services.
 * 
 * USAGE EXAMPLE:
 * var serviceFactory = system.ActorOf(LoginServiceFactory.Props(), "loginServiceFactory");
 * 
 * NOTE:
 * These services are automatically attached to `SessionActor`s that join the login server
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Login.Services;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Services;

namespace Imlight.CoreLib.Login;

public class LoginServiceFactory : ServiceFactory {

    protected override HashSet<Type> ServiceTypes { get; set; } = [
        typeof(ControlService),
        typeof(AccountService),
        typeof(AuthenticatorService),
        typeof(CharacterService),
        typeof(GameTransitionService),
        typeof(LoginAFKService),
    ];

    public static Props Props() 
        => Akka.Actor.Props.Create(() => new LoginServiceFactory());

}
