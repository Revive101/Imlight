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
public class ServerWizSpellbookBehavior : ServerSpellbookBehavior {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    // This is all we need to store in the database.
    public List<uint> SpellTemplateIdList = new();

    public void Initialize() {
        // Dragon database only keeps track of spell template IDs. It's up to this
        // behavior to convert those IDs into actual spell objects.
        if (SpellTemplateIdList is null) {
            return;
        }

        Spells = new List<Spell>();
        foreach (var templateId in SpellTemplateIdList) {
            var spell = SpellFactory.CreateSpellFromTemplate(templateId);
            if (spell != null) {
                Spells.Add(spell);
            }
        }
    }

    public override void LearnSpell(Spell spell) {
        base.LearnSpell(spell);

        SpellTemplateIdList ??= new List<uint>();
        SpellTemplateIdList.Add(spell.m_templateID);
    }

    public override void UnlearnSpell(uint templateId) {
        base.UnlearnSpell(templateId);

        if (SpellTemplateIdList is null) {
            return;
        }

        SpellTemplateIdList.Remove(templateId);
    }

    public void AddSpell(Spell spell) {
        TemporarySpells ??= new List<Spell>();

        if (spell != null) {
            TemporarySpells.Add(spell);
        }
    }

    public void RemoveSpell(uint templateId) {
        if (TemporarySpells is null) {
            return;
        }

        var spell = TemporarySpells.Find(x => x.m_templateID == templateId);
        if (spell != null) {
            TemporarySpells.Remove(spell);
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
