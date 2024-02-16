/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Implementations;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerSpellbookBehavior : BehaviorInstance, IClientBehaviorProvider<ClientSpellbookBehavior> {
    public List<SpellIDTracker> SpellIdList;

    public ClientSpellbookBehavior GetClientBehaviorInstance() {
        return new ClientSpellbookBehavior {
            m_spellIDList = SpellIdList
        };
    }
}
