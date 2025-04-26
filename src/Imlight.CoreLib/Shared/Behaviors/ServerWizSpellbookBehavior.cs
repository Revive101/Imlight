/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.CoreLib.Game.Spells;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizSpellbookBehavior : ServerSpellbookBehavior {

    [JsonIgnore] public new bool NoTransfer { get; set; } = false;

    [JsonIgnore] public MagicSchool PrimarySchool { get; set; }
    [JsonIgnore] public int GenericMaxRank { get; set; }
    [JsonIgnore] public int SchoolMaxRank { get; set; }
    [JsonIgnore] public int GenericMaxInstances { get; set; }
    [JsonIgnore] public int SchoolMaxInstances { get; set; }
    [JsonIgnore] public int MaxSpells { get; set; }
    [JsonIgnore] public int MaxTreasureCards { get; set; }

    public void InitializeProperties(DeckBehaviorTemplate deckTemplate) {
        SetPropertiesFromDeckTemplate(deckTemplate);
    }

    public void InitializeSpells(DeckBehavior deckBehavior) {
        if (deckBehavior is null) {
            return;
        }

        base.SpellList = deckBehavior.m_spellList;
    }

    public bool EquipDeck(WizItemTemplate template, DeckBehavior deckBehavior) {
        if (template is null) {
            return false;
        }

        // Search for a deck behavior template within the item template.
        foreach (var behaviorTemplate in template.m_behaviors) {
            if (behaviorTemplate is not DeckBehaviorTemplate deckBehaviorTemplate) {
                continue;
            }

            // We've found what we're looking for. Set the deck behavior properties.
            SetPropertiesFromDeckTemplate(deckBehaviorTemplate);
            base.SpellList = deckBehavior.m_spellList;

            return true;
        }

        return false;
    }

    public bool AddSpellToDeck(uint spellTemplateId) {
        base.SpellList ??= new List<SpellData>();

        if (TotalSpellCount() >= MaxSpells) {
            Logger.Debug("The deck already has the maximum amount of allowed spells.");
            
            return false;
        }

        // Get the spells template; we'll need it for the magic school ID.
        var spellTemplate = SpellFactory.GetSpell(spellTemplateId);
        if (spellTemplate is null) {
            Logger.Debug("Failed to create spell from template {0}.", Logger.Args(spellTemplateId));
            
            return false;
        }

        // Create a new SpellData for this spell, if one doesn't already exist.
        var spellData = SpellList.Find(x => x.m_templateID == spellTemplateId);
        if (spellData is null) {
            // If the spell doesn't exist in the deck, we'll want to add it.
            spellData = new SpellData {
                m_templateID = spellTemplateId,
                m_quantity = 1
            };
            SpellList.Add(spellData);
        }
        else {
            // Otherwise, we'll want to increase the quantity so long as the number of max instances hasn't been reached.
            var spellSchool = (MagicSchool) spellTemplate.m_magicSchoolID;
            var maxInstances = spellSchool == PrimarySchool ? SchoolMaxInstances : GenericMaxInstances;
            if (spellData.m_quantity >= maxInstances) {
                Logger.Debug("The deck already has the maximum amount of allowed instances of spell {0}.", Logger.Args(spellTemplateId));
                
                return false;
            }

            spellData.m_quantity++;
        }

        return true;
    }

    public bool RemoveSpellFromDeck(uint spellTemplateId) {
        if (SpellList is null) {
            return false;
        }

        var spellData = SpellList.Find(x => x.m_templateID == spellTemplateId);
        if (spellData is null) {
            Logger.Debug("The deck does not contain spell {0}.", Logger.Args(spellTemplateId));
            return false;
        }

        // Decrease the quantity, if we can. Otherwise, remove the spell data.
        if (spellData.m_quantity - 1 <= 0) {
            SpellList.Remove(spellData);
        }
        else {
            spellData.m_quantity--;
        }

        return true;
    }

    public new ClientSpellbookBehavior GetClientBehaviorInstance() {
        var spellIdList = new List<SpellIDTracker>();
        foreach (var templateId in LearnedSpellTemplateIds) {
            spellIdList.Add(new SpellIDTracker {
                m_isRetired = false,
                m_spellID = templateId,
            });
        }

        return new ClientSpellbookBehavior {
            m_spellIDList = spellIdList
        };
    }

    private void SetPropertiesFromDeckTemplate(DeckBehaviorTemplate template) {
        // Set the deck behavior properties.
        // Try to parse the string school as a MagicSchool enum.
        MagicSchool school;
        if (string.IsNullOrEmpty(template.m_primarySchoolName)
            || !Enum.TryParse(template.m_primarySchoolName, true, out school)) {
            school = MagicSchool.None;
        }
        this.PrimarySchool = school;
        this.GenericMaxRank = template.m_genericMaxRank;
        this.SchoolMaxRank = template.m_schoolMaxRank;
        this.GenericMaxInstances = template.m_genericMaxInstances;
        this.SchoolMaxInstances = template.m_schoolMaxInstances;
        this.MaxSpells = template.m_maxSpells;
        this.MaxTreasureCards = template.m_maxTreasureCards;
    }
    
}
