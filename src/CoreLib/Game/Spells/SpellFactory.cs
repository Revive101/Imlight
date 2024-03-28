/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Spells;

public static class SpellFactory {
    /// <summary>
    /// Creates an array of spells based on the provided spell effect.
    /// </summary>
    /// <param name="effect">The spell effect used to create the spells.</param>
    /// <returns>An array of spells created from the spell effect.</returns>
    public static Spell[] CreateSpellsFromEffect(ProvideSpellEffect effect) {
        // The spell template does not include the template ID. We need both to create the spell.
        var spellTemplatePath = $"Spells/{effect.m_spellName}.xml";
        var spellTemplate = RootArchiveLoader.GetFile<SpellTemplate>(spellTemplatePath);
        if (spellTemplate is null) {
            // The spell may be in tiered spells directory.
            spellTemplatePath = $"Spells/Tiered Spells/{effect.m_spellName}.xml";
            spellTemplate = RootArchiveLoader.GetFile<SpellTemplate>(spellTemplatePath);

            if (spellTemplate is null) {
                Logger.Warning("Could not find spell template {0}.", Logger.Args(effect.m_spellName));
                return null;
            }
        }

        var spellTemplateId = CoreObjectFactory.GetCoreTemplateID(spellTemplatePath);
        var spell = CreateSpellFromTemplate(spellTemplateId);

        var spells = new List<Spell>();
        for (var i = 0; i < effect.m_numSpells; i++) {
            // Indicate to the client that this spell is given by an item.
            spell.m_itemCard = true;

            spells.Add(spell);
        }

        return spells.ToArray();
    }

    /// <summary>
    /// Creates a spell from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the spell template.</param>
    /// <returns>The created spell object.</returns>
    public static Spell CreateSpellFromTemplate(uint templateId) {
        var template = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        if (template == null) {
            Logger.Warning("Tried to create spell from non-existent template {0}.", Logger.Args(templateId));
            return null;
        }
        if (template is not SpellTemplate spellTemplate) {
            Logger.Warning("Tried to create spell from non-spell template {0}.", Logger.Args(templateId));
            return null;
        }

        // Create a random uint32 for the spell ID.
        var ran = new Random();
        var spellId = (uint) ran.Next(0, int.MaxValue);

        var spell = new Spell() {
            m_templateID = templateId,
            m_pipCost = template.m_spellRank,
            m_accuracy = (byte) template.m_accuracy,
            m_treasureCard = template.m_Treasure,
            m_spellID = spellId,
            m_itemCard = true,
            m_magicSchoolID = StringHash.Compute(spellTemplate.m_sMagicSchoolName),
        };

        return spell;
    }

    /// <summary>
    /// Retrieves the name of a spell based on its template ID.
    /// </summary>
    /// <param name="templateId">The template ID of the spell.</param>
    /// <returns>The name of the spell, or null if the template is not found.</returns>
    public static string GetSpellName(uint templateId) {
        var template = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);
        return template?.m_name;
    }

    /// <summary>
    /// Retrieves the base spell name for a given template ID.
    /// </summary>
    /// <param name="templateId">The ID of the spell template.</param>
    /// <returns>The base spell name, or null if the template is not found.</returns>
    public static string GetBaseSpellName(uint templateId) {
        var template = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);
        return template?.m_spellBase;
    }
}
