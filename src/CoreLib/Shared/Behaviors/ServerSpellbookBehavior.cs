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

public class ServerSpellbookBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    [JsonIgnore] public List<Spell> Spells = new();
    [JsonIgnore] public List<Spell> TemporarySpells = new(); // Spells gained from equipment

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

    public override ClientSpellbookBehavior GetClientBehaviorInstance() {
        var spellIdList = new List<SpellIDTracker>();
        foreach (var spell in Spells) {
            spellIdList.Add(new SpellIDTracker {
                m_isRetired = false,
                m_spellID = spell.m_templateID,
            });
        }

        return new ClientSpellbookBehavior {
            m_spellIDList = spellIdList
        };
    }
}
