/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerSpellbookBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    public List<uint> LearnedSpellTemplateIds = new();
    [JsonIgnore] public List<SpellData> SpellList = new();
    [JsonIgnore] public List<Spell> TemporarySpells = new();

    public virtual void AddSpellToBook(Spell spell) {
        if (LearnedSpellTemplateIds.Contains(spell.m_templateID)) {
            return;
        }

        LearnedSpellTemplateIds.Add(spell.m_templateID);
    }

    public virtual void RemoveSpellFromBook(uint templateId) {
        LearnedSpellTemplateIds.Remove(templateId);

        if (LearnedSpellTemplateIds is null) {
            return;
        }

        var spellTemplateId = LearnedSpellTemplateIds.Find(x => x == templateId);
        if (spellTemplateId != 0) {
            LearnedSpellTemplateIds.Remove(spellTemplateId);
        }
    }

    public void AddTemporarySpellToBook(Spell spell) {
        TemporarySpells ??= new List<Spell>();

        if (spell != null) {
            TemporarySpells.Add(spell);
        }
    }

    public void RemoveTemporarySpellFromBook(uint templateId) {
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
        foreach (var spellTemplateId in LearnedSpellTemplateIds) {
            spellIdList.Add(new SpellIDTracker {
                m_spellID = spellTemplateId
            });
        }

        return new ClientSpellbookBehavior {
            m_spellIDList = spellIdList
        };
    }

    public int TotalSpellCount() {
        if (SpellList is null) {
            return 0;
        }

        return SpellList.Sum(spellData => (int) spellData.m_quantity);
    }
}
