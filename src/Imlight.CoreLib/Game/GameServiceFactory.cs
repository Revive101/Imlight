/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * GAME SERVICE FACTORY 
 * ========================================================================
 * 
 * PURPOSE:
 * Defines and registers the service types required for game operations,
 * providing a centralized factory for creating game-related services.
 * 
 * USAGE EXAMPLE:
 * var serviceFactory = system.ActorOf(GameServiceFactory.Props(), "gameServiceFactory");
 * 
 * NOTE:
 * These services are automatically attached to any `SessionActor` that joins the game server.
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
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Services;

namespace Imlight.CoreLib.Game;

public class GameServiceFactory : ServiceFactory {

    protected override HashSet<Type> ServiceTypes { get; set; } = [
        typeof(ControlService),
        typeof(AttachService),
        typeof(AccountService),
        typeof(ClientService),
        typeof(MoveService),
        typeof(ZoneService),
        typeof(WizardService),
        typeof(ChatService),
        typeof(SpellbookService),
        typeof(InventoryService),
        typeof(EquipmentService),
        typeof(CommandService),
        typeof(CombatService),
        typeof(InteractService),
        typeof(ShopService),
        typeof(AuctionHouseService),
        typeof(DynaModService),
        typeof(CantripService),
        typeof(TrainService),
        typeof(PetService),
        typeof(MinigameService),
        typeof(FriendsService),
    ];

    public static Props Props() 
        => Akka.Actor.Props.Create(() => new GameServiceFactory());

}
