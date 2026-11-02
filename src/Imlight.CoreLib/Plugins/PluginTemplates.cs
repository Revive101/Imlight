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
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/01/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Resources;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Turns game templates into the plugin API's records.
/// </summary>
internal static class PluginTemplates {

    /// <summary>
    /// The item with this template ID, or null if the ID does not name an item.
    /// </summary>
    public static ItemInfo GetItem(ulong templateId)
        => CoreObjectFactory.FindCoreTemplate(templateId) is WizItemTemplate item ? ToItemInfo(templateId, item) : null;

    /// <summary>
    /// Every item whose template file has this name.
    /// </summary>
    public static IReadOnlyList<ItemInfo> FindItems(string name)
        => [.. CoreObjectFactory.GetTemplateIdsByObjectName(name).Select(GetItem).Where(x => x is not null)];

    /// <summary>
    /// The spell with this template ID, or null if the ID does not name a spell.
    /// </summary>
    public static SpellInfo GetSpell(uint templateId)
        => CoreObjectFactory.FindCoreTemplate(templateId) is SpellTemplate spell ? ToSpellInfo(templateId, spell) : null;

    /// <summary>
    /// The spell with this internal name, or null if there is none.
    /// </summary>
    public static SpellInfo GetSpell(string name) {
        var templateId = SpellFactory.GetTemplateIdByHash(StringHash.Compute(name));

        return templateId == 0 ? null : GetSpell(templateId);
    }

    /// <summary>
    /// The English name of a template, falling back to its internal name, or an empty string if it has neither.
    /// </summary>
    public static string ResolveName(CoreTemplate template) {
        if (template is GameObjectTemplate gameObjectTemplate) {
            var displayName = Locale.GetEnglishName(gameObjectTemplate.m_displayName);
            if (!string.IsNullOrEmpty(displayName)) {
                return displayName;
            }

            string objectName = gameObjectTemplate.m_objectName;
            if (!string.IsNullOrEmpty(objectName)) {
                return objectName;
            }
        }

        return "";
    }

    private static ItemInfo ToItemInfo(ulong templateId, WizItemTemplate item)
        => new(templateId, ResolveName(item), item.m_objectName, PluginSchool.ToPluginSchool(item.m_school));

    private static SpellInfo ToSpellInfo(uint templateId, SpellTemplate spell) {
        string name = spell.m_name;
        var displayName = Locale.GetEnglishName(spell.m_displayName);

        return new SpellInfo(templateId,
            name,
            string.IsNullOrEmpty(displayName) ? name : displayName,
            PluginSchool.ToPluginSchool(spell.m_sMagicSchoolName),
            spell.m_spellRank?.m_spellRank ?? 0,
            spell.m_accuracy,
            spell.m_Treasure);
    }

}
