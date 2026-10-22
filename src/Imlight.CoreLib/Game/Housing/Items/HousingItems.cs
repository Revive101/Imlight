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
 * Decides from an item's own template whether it may be placed in a house, and how many copies of one item a house
 * or a backpack may hold.
 *
 * USAGE EXAMPLE:
 * switch (HousingItems.Check(templateId, out var template, out var family)) {
 *     case HousingPlacement.Allowed: // place it
 *     case HousingPlacement.NotEnabled: // the family is declared but not switched on
 *     default: // the client never offers this item
 * }
 *
 * NOTE:
 * - The rule is the client's: it offers an item for placing when its template is an object that is not a weapon and
 *   carries the Housing adjective (ClientUtility get_item_category_name 0x140463250, FurniturePageWindow::IsFurnitureItem
 *   0x140fa56d0), and it empties a blob entry whose template has a seed behavior (BaseClientHousingBlobStrategy::CreateObject
 *   0x1422170f0). Which folder a template lives in is not part of it.
 * - A pet or a mount is furniture by its behavior, with or without the adjective.
 * - Plants, wallpapers and seeds go through their own messages, so they are not furniture.
 * - Furniture of a family that is declared but not enabled is refused without blame, because the client offers it.
 * - A placed or stored object keeps its item document, so it comes back as the same item. An object that has no document, as
 *   one a layout placed, or whose document is gone, comes back as a new item of its template.
 *
 * TODO:
 * - Is the client's rule for offering a pet or a mount for placing the behavior, as it is here? No capture shows it.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Families;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Items;

/// <summary>
/// Whether an item template is furniture.
/// </summary>
internal enum HousingItemKind {
    NotFurniture,
    Furniture,
}

/// <summary>
/// What the server does with a request to place an item.
/// </summary>
internal enum HousingPlacement {
    Allowed,
    NotEnabled,
    Refused,
}

internal static class HousingItems {

    private const string FURNITURE_ADJECTIVE = "Housing";
    private const string WEAPON_ADJECTIVE = "Weapon";

    private static readonly StripedLocks s_backpackLocks = new();

    /// <summary>
    /// Gets the lock that keeps one character's backpack list from changing in two places at once while house items are
    /// issued, placed, picked up and moved to or from the attic.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <returns>The lock to hold for as long as the backpack is read and then changed.</returns>
    internal static Lock BackpackLock(Wizard wizard) => s_backpackLocks.For(wizard.CharId);

    /// <summary>
    /// Checks the rules every placed object follows, whatever its family: the template is an object the client offers
    /// for placing and is not a seed.
    /// </summary>
    /// <param name="template">An item template.</param>
    /// <returns><see cref="HousingItemKind.Furniture"/> when the template passes.</returns>
    internal static HousingItemKind Classify(WizItemTemplate template) {
        if (template is null) {
            return HousingItemKind.NotFurniture;
        }

        if (IsPetOrMount(template) && !IsSeed(template)) {
            return HousingItemKind.Furniture;
        }

        if (template.m_nObjectType != ObjectType.OT_OBJECT) {
            return HousingItemKind.NotFurniture;
        }

        var adjectives = (template.m_adjectiveList ?? []).Select(adjective => adjective.ToString()).ToList();
        if (!adjectives.Contains(FURNITURE_ADJECTIVE, StringComparer.Ordinal)
            || adjectives.Contains(WEAPON_ADJECTIVE, StringComparer.Ordinal)) {
            return HousingItemKind.NotFurniture;
        }

        return IsSeed(template) ? HousingItemKind.NotFurniture : HousingItemKind.Furniture;
    }

    /// <summary>
    /// Finds the attic blob subtype of an item template: the subtype of its family, or the plain one when the client has no
    /// attic blob for the family.
    /// </summary>
    /// <param name="template">An item template.</param>
    /// <returns>The subtype of the blob the template's items are in.</returns>
    internal static string AtticSubTypeOf(WizItemTemplate template)
        => FamilyOf(template).AtticSubType ?? HousingFamilies.Plain.AtticSubType;

    /// <summary>
    /// Finds the template of an item and decides whether the server places it.
    /// </summary>
    /// <param name="templateId">The item's template id.</param>
    /// <param name="template">The item template when it exists and is furniture.</param>
    /// <param name="family">
    /// The family that decided: the plain family when the item is allowed, the first family that is not enabled
    /// otherwise. <c>null</c> when the item is refused.
    /// </param>
    /// <returns>
    /// <see cref="HousingPlacement.Refused"/> for an item the client never offers, <see cref="HousingPlacement.NotEnabled"/>
    /// for furniture of a family the server does not support yet.
    /// </returns>
    internal static HousingPlacement Check(uint templateId, out WizItemTemplate template, out HousingFamily family) {
        template = null;
        family = null;
        if (CoreObjectFactory.GetCoreTemplate(templateId) is not WizItemTemplate item || Classify(item) != HousingItemKind.Furniture) {
            return HousingPlacement.Refused;
        }

        template = item;
        family = FamilyOf(item);

        return family.Enabled ? HousingPlacement.Allowed : HousingPlacement.NotEnabled;
    }

    /// <summary>
    /// Checks whether a template may not have another copy: the client's limit on how often a house may hold
    /// one item, and how many copies a backpack may hold of it.
    /// </summary>
    /// <param name="template">The item template.</param>
    /// <param name="copies">The copies already held.</param>
    /// <returns><c>true</c> when the template has a limit and <paramref name="copies"/> reaches it.</returns>
    internal static bool TemplateCopyLimitReached(WizItemTemplate template, int copies)
        => template.m_itemLimit > 0 && copies >= template.m_itemLimit;

    /// <summary>
    /// Counts the copies of a template in a backpack.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <param name="templateId">The item template.</param>
    /// <returns>The number of items of the template.</returns>
    internal static int CountInBackpack(Wizard wizard, uint templateId)
        => wizard.InventoryBehavior.Items.Count(item => item.m_templateID == templateId);

    /// <summary>
    /// Creates the backpack item for an object that is picked up. The item is not added to the backpack.
    /// </summary>
    /// <param name="wizard">The character who picks the object up.</param>
    /// <param name="templateId">The object's template id.</param>
    /// <returns>The new item, or <c>null</c> when the template is not an item.</returns>
    internal static WizClientObjectItem CreateItem(Wizard wizard, uint templateId) {
        if (CoreObjectFactory.FinalizeCoreObject(templateId) is not WizClientObjectItem item) {
            return null;
        }

        item.m_characterId = (GID) wizard.CharId;
        CoreObjectFactory.InitializeCoreObjectBehaviors(item, item.m_templateID);

        return item;
    }

    /// <summary>
    /// Takes the item of an object that leaves a house or an attic. The item is not added to the backpack.
    /// </summary>
    /// <param name="wizard">The character who takes the object.</param>
    /// <param name="templateId">The object's template id.</param>
    /// <param name="itemGid">The item document the object was placed from, or 0 for an object that has none.</param>
    /// <param name="created">
    /// <c>true</c> when the item is new and its document must be stored with the change that moves it; <c>false</c> when the
    /// stored document of the object is used.
    /// </param>
    /// <returns>The item, or <c>null</c> when the document could not be read or the template is not an item.</returns>
    internal static WizClientObjectItem TakeItem(Wizard wizard, uint templateId, ulong itemGid, out bool created) {
        created = false;
        if (itemGid != 0) {
            try {
                if (WizardItemCollection.Load(wizard.CharId, itemGid) is { } stored) {
                    return stored;
                }
            } catch (Exception exception) {
                Logger.Warning("Could not load the stored item {ItemId} of character {CharId}: {Reason}",
                    Logger.Args(itemGid, wizard.CharId, exception.Message));

                return null;
            }

            Logger.Warning("Item {ItemId} of character {CharId} has no stored document, so template {TemplateId} comes back as a new item.",
                Logger.Args(itemGid, wizard.CharId, templateId));
        }

        created = true;

        return CreateItem(wizard, templateId);
    }

    private static bool IsSeed(WizItemTemplate template)
        => (template.m_behaviors ?? []).OfType<SeedBehaviorTemplate>().Any();

    private static HousingFamily FamilyOf(WizItemTemplate template) {
        var families = HousingFamilies.Of(template);

        return families.FirstOrDefault(candidate => !candidate.Enabled) ?? families[0];
    }

    private static bool IsPetOrMount(WizItemTemplate template)
        => HousingFamilies.Of(template).Any(family => family == HousingFamilies.Pet || family == HousingFamilies.Mount);

}
