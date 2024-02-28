/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Spells;

public static class SpellFactory {
    /// <summary>
    /// Creates a spell from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the spell template.</param>
    /// <returns>The created spell object.</returns>
    public static Spell CreateSpellFromTemplate(uint templateId) {
        var template = CoreObjectFactory.GetCoreTemplate(templateId);

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
            m_pipCost = spellTemplate.m_spellRank,
            m_accuracy = (byte) spellTemplate.m_accuracy,
            m_spellEffects = spellTemplate.m_effects,
            m_treasureCard = spellTemplate.m_Treasure,
            m_spellID = spellId,
        };

        return spell;
    }
}
