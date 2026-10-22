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
 * ========================================================================
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * The two lookups of a character's lots that travel, visits and attaching all need: the house a character stands in,
 * and a character's lot in a zone.
 *
 * USAGE EXAMPLE:
 * if (HouseLots.TryLoadAttached(host, out var house, out var template)) {
 *     // house is the stored house the host stands in
 * }
 *
 * NOTE:
 * - A character has one lot per house template, so a zone shared by several templates is searched in template order.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Access;

internal static class HouseLots {

    /// <summary>
    /// Loads the stored house a character stands in, and its template.
    /// </summary>
    /// <param name="wizard">The character; one that stands in no house has no result.</param>
    /// <param name="house">The stored house when this returns <c>true</c>.</param>
    /// <param name="template">The template of the house when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the character is in no house, or the house is not stored or has no known template.</returns>
    internal static bool TryLoadAttached(Wizard wizard, out House house, out HouseTemplateInfo template) {
        house = null;
        template = default;
        if (wizard.AttachedLotGid == 0) {
            return false;
        }

        var stored = HouseCollection.Load(wizard.AttachedLotGid);
        if (stored is null || !HouseTemplates.TryGet(stored.TemplateId, out template)) {
            Logger.Warning("Character {CharId} stands in lot {LotGid}, which is not a stored house.", Logger.Args(wizard.CharId, wizard.AttachedLotGid));

            return false;
        }

        house = stored;

        return true;
    }

    /// <summary>
    /// Finds a character's lot in a house zone.
    /// </summary>
    /// <param name="owner">The character whose lots are searched.</param>
    /// <param name="zone">The house zone.</param>
    /// <param name="template">The template of the lot when this returns <c>true</c>.</param>
    /// <param name="lotGid">The lot when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the character has no lot in the zone.</returns>
    internal static bool TryFindInZone(Wizard owner, string zone, out HouseTemplateInfo template, out ulong lotGid) {
        foreach (var (templateId, lot) in owner.HouseLots.OrderBy(pair => pair.Key)) {
            if (HouseTemplates.TryGet(templateId, out template) && string.Equals(template.LotZone, zone, StringComparison.OrdinalIgnoreCase)) {
                lotGid = lot;

                return true;
            }
        }

        template = default;
        lotGid = 0;

        return false;
    }

}
