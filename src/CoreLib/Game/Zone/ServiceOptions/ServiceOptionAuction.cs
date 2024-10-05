/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionAuction : ServiceOption {
    public override string ServiceName { get; protected set; } = "AuctionHouseService";
    public override string WizBang { get; set; } = "Shopping";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_NPCInteractText";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new AuctionHouseOption() {
            m_auctionHousePurchaseKey = 1, // Todo: Find out what this is
            m_displayKey = "GUI_AuctionHouse",
            m_forceInteract = false,
            m_iconKey = "Shopping",
            m_serviceIndex = 0,
            m_serviceName = "AuctionHouseService"
        }
    };

    public ServiceOptionAuction(CoreObject ActiveGameObject) : base(ActiveGameObject)
        => RecalculateOnProximityEnter = false;

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
        // We've already sent the service option and need to do nothing more.
        // If you're looking for Auction house interaction, you'll find it in Game/Services/AuctionHouseService.cs
    }

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect)
        => ServiceOptionBases;
}
