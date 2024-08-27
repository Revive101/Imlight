/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionDyes : ServiceOption {
    public override string ServiceName { get; protected set; } = "DyeShopService";
    public override string WizBang { get; set; } = "Shopping";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_NPCInteractText";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new DyeShopOption() {
            m_displayKey = "GUI_DyeShop",
            m_forceInteract = false,
            m_iconKey = "DyeShop",
            m_serviceIndex = 0,
            m_serviceName = "DyeShopService"
        }
    };

    public ServiceOptionDyes(CoreObject ActiveGameObject) : base(ActiveGameObject)
        => RecalculateOnProximityEnter = false;

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
        var dyeShopOpen = new WIZARD_12_PROTOCOL.MSG_DYESHOPOPEN() {
            GlobalID = ActiveGameObject.m_globalID,
            Title = "WC-NPCs_00000718"
        };

        suspect.Tell(dyeShopOpen);
    }

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect)
        => ServiceOptionBases;
}
