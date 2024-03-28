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

[Serializable]
public class ServerWizSpellbookBehavior : ServerSpellbookBehavior, IClientBehaviorProvider<ClientSpellbookBehavior> {
    public List<uint> SpellTemplateIdList;

    [JsonIgnore] public List<uint> TemporarySpellTemplateIdList = new(); // Spells gained from equipment

    public void Initialize() {
        // Dragon database only keeps track of spell template IDs. It's up to this
        // behavior to convert those IDs into actual spell objects.
        Spells = new List<Spell>();
        foreach (var templateId in SpellTemplateIdList) {
            var spell = SpellFactory.CreateSpellFromTemplate(templateId);
            if (spell != null) {
                Spells.Add(spell);
            }
        }
    }

    public void AddSpell(Spell spell) {
        Spells ??= new List<Spell>();

        if (spell != null) {
            Spells.Add(spell);
            TemporarySpellTemplateIdList.Add(spell.m_templateID);
        }
    }

    public void RemoveSpell(uint templateId) {
        if (Spells is null) {
            return;
        }

        var spell = Spells.Find(x => x.m_templateID == templateId);
        if (spell != null) {
            Spells.Remove(spell);
            SpellTemplateIdList.Remove(spell.m_templateID);
        }
    }

    public ClientSpellbookBehavior GetClientBehaviorInstance() {
        var spellIdList = new List<SpellIDTracker>();
        foreach (var spell in Spells) {
            spellIdList.Add(new SpellIDTracker {
                m_isRetired = false,
                m_spellID = spell.m_spellID,
            });
        }

        return new ClientSpellbookBehavior {
            m_spellIDList = spellIdList
        };
    }
}
