/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerSpellbookBehavior : IClientBehaviorProvider<ClientSpellbookBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public List<uint> LearnedSpellTemplateIds = [];
    public List<uint> TreasureCardTemplateIds = [];
    [JsonIgnore] public List<SpellData> SpellList = [];
    [JsonIgnore] public List<Spell> TemporarySpells = [];

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
        TemporarySpells ??= [];

        if (spell != null) {
            TemporarySpells.Add(spell);
        }
    }

    public void RemoveTemporarySpellFromBook(uint stringHash) {
        if (TemporarySpells is null) {
            return;
        }

        var spell = TemporarySpells.Find(x => x.m_spellID == stringHash);
        if (spell != null) {
            TemporarySpells.Remove(spell);
        }
    }

    public bool HasSpell(uint templateId) 
        => LearnedSpellTemplateIds.Contains(templateId);

    public void AddTreasureCard(uint templateId) {
        TreasureCardTemplateIds ??= [];
        TreasureCardTemplateIds.Add(templateId);
    }

    public bool RemoveTreasureCard(uint templateId) {
        if (TreasureCardTemplateIds is null) {
            return false;
        }

        return TreasureCardTemplateIds.Remove(templateId);
    }

    public int TreasureCardCount(uint templateId) {
        if (TreasureCardTemplateIds is null) {
            return 0;
        }

        return TreasureCardTemplateIds.Count(x => x == templateId);
    }

    public ClientSpellbookBehavior GetClientBehaviorInstance() {
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
