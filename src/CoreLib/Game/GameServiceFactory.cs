/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Services;

namespace Imlight.CoreLib.Game;

public class GameServiceFactory : ServiceFactory {
    protected override HashSet<Type> ServiceTypes { get; set; } = new HashSet<Type>()
    {
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
        typeof(TrainService)
    };

    public static Props Props() {
        return Akka.Actor.Props.Create(() => new GameServiceFactory());
    }
}
