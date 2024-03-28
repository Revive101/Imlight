/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.Game.Spells;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerSpellbookBehavior : BehaviorInstance {
    [JsonIgnore] public List<Spell> Spells = new();

    public void LearnSpell(Spell spell) {
        Spells ??= new List<Spell>();

        if (spell != null) {
            Spells.Add(spell);
        }
    }

    public void UnlearnSpell(uint templateId) {
        if (Spells is null) {
            return;
        }

        var spell = Spells.Find(x => x.m_templateID == templateId);
        if (spell != null) {
            Spells.Remove(spell);
        }
    }
}
