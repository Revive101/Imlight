/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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
