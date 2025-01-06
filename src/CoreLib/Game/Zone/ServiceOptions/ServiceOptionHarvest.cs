/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Reagents;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;
internal class ServiceOptionHarvest : ServiceOption {
    public override string ServiceName { get; protected set; } = "ReagentService";
    public override string WizBang { get; set; } = "None";
    public override string NpcIconOverride { get; protected set; } = "GUI/Buttons/Button_Spiral.dds";
    public override string NpcNameKeyOverride { get; protected set; } = "WizardGameObjects_00000070";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_00004805";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new InteractableOption {
            m_displayKey = "GUI_UniverseMap",
            m_forceInteract = false,
            m_iconKey = "UniverseMap",
            m_serviceIndex = 0,
            m_serviceName = "ReagentService"
        }
    };

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
        Logger.Debug("Ding!");
    }

    public ServiceOptionHarvest(CoreObject ActiveGameObject) : base(ActiveGameObject)
        => RecalculateOnProximityEnter = false;

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect)
        => ServiceOptionBases;
}
