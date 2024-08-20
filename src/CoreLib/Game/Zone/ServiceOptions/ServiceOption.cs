/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public abstract class ServiceOption {
    public abstract string ServiceName { get; protected set;}
    public abstract string WizBang { get; set; }
    public abstract List<ServiceOptionBase> ServiceOptionBases { get; set; }
    public virtual string NpcIconOverride { get; protected set; }
    public virtual string NpcNameKeyOverride { get; protected set; }
    public virtual string NpcTextKeyOverride { get; protected set; }
    protected CoreObject ActiveGameObject { get; private set; }

    // ctor
    public ServiceOption(CoreObject activeGameObject) {
        // Check to see if the WizBang is valid.
        if (WizBang is null || WizBangs.DoesWizBangExist(WizBang)) {
            Logger.Error("Invalid WizBang: {0}", Logger.Args(WizBang));
        }

        this.ActiveGameObject = activeGameObject;
    }

    public abstract void OnPlayerInteraction(IActorRef suspect, int serviceIndex);
}
