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
 * Gives characters their houses. A house is an item whose template has a DeedBehaviorTemplate, and each one a character
 * owns has a lot of its own, which the client reads from the item. This issues those items, finds the house Home goes
 * to, and keeps every house item pointing at its lot.
 *
 * USAGE EXAMPLE:
 * if (HouseDeeds.TryPrepareHome(wizard, out var entry, out var failure)) {
 *     // send the player to entry.Zone, lot entry.LotGid
 * }
 * var grant = HouseDeeds.Issue(wizard, templateId);
 *
 * NOTE:
 * - A character has one lot per house template, in Wizard.HouseLots. The pointer is written before the item exists,
 *   so an item that is trashed or traded is issued again with the same lot and the house keeps its furniture.
 * - A deed that names a structure is issued together with it, each item with its own lot.
 * - A character's backpack lock (HousingItems.BackpackLock) covers issuing and every handler that moves a house item between the
 *   backpack, a house and the attic. Take it before any house or attic state, never inside one.
 * - Running an issue again changes nothing. Nothing is created when the backpack cannot hold everything that is
 *   missing, and a lot that was stored stays stored.
 * - The house document is not created by an issue. The first entry creates it from the template's layout.
 * - Home goes to the first starter template that is a house (Housing.StarterDeeds). A starter is a plain house template
 *   like any other; no data marks it.
 *
 * TODO:
 * - The equipped Islands item should decide the home. The slots exist, but which lot of an equipped castle Home goes to
 *   needs a capture.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Access;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// Why a house could not be issued.
/// </summary>
internal enum HouseGrantFailure {
    None,
    NotAHouse,
    BackpackFull,
    Unavailable,
}

/// <summary>
/// The outcome of issuing houses.
/// </summary>
/// <param name="Failure">Why nothing was issued, or <see cref="HouseGrantFailure.None"/>.</param>
/// <param name="Created">The items this call created, which the client does not know yet.</param>
internal readonly record struct HouseGrant(HouseGrantFailure Failure, IReadOnlyList<WizClientObjectItem> Created) {

    internal bool Succeeded => Failure == HouseGrantFailure.None;

}

/// <summary>
/// The house Home goes to.
/// </summary>
/// <param name="TemplateId">The template of the house, or 0 when the character has no home to go to.</param>
/// <param name="Lot">The character's lot for the template, or 0 when none was issued yet.</param>
/// <param name="Item">The house item the character holds, or <c>null</c> when it is missing.</param>
internal readonly record struct HomeTarget(uint TemplateId, ulong Lot, WizClientObjectItem Item);

internal static class HouseDeeds {

    /// <summary>
    /// The location a character arrives at in a house zone. Every house zone defines it.
    /// </summary>
    internal const string ENTRY_LOCATION = "Start";

    /// <summary>
    /// Finds the house Home goes to.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <returns>The first starter house, with what the character holds of it; the target has template 0 when no starter is a house.</returns>
    internal static HomeTarget ResolveHome(Wizard wizard) {
        var starters = HouseTemplates.Starters();
        if (starters.Count == 0) {
            return default;
        }

        var templateId = starters[0].TemplateId;
        var lot = wizard.HouseLots.GetValueOrDefault(templateId);
        var item = FindItem(wizard, templateId);

        return new HomeTarget(templateId, lot, item);
    }

    /// <summary>
    /// Makes sure the character holds every starter house, each with its lot.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <returns>The items that were created. Calling it again creates none.</returns>
    internal static HouseGrant EnsureStarter(Wizard wizard) {
        var starters = HouseTemplates.Starters();
        if (starters.Count == 0) {
            Logger.Error("No template of Housing.StarterDeeds is a house.");

            return new HouseGrant(HouseGrantFailure.Unavailable, []);
        }

        return IssueAll(wizard, [.. starters.Select(starter => starter.TemplateId)]);
    }

    /// <summary>
    /// Gives a character a house, and its structure when the template names one.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <param name="templateId">The deed or structure template.</param>
    /// <returns>The items that were created; none for a house the character already holds.</returns>
    internal static HouseGrant Issue(Wizard wizard, uint templateId)
        => IssueAll(wizard, [templateId]);

    /// <summary>
    /// Gets a character ready to enter their home: the starter items exist, and the house is stored.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <param name="entry">What the zone transfer needs when this returns <c>true</c>.</param>
    /// <param name="failure">Why the character cannot go home when this returns <c>false</c>.</param>
    /// <returns><c>false</c> when the backpack cannot hold a missing item or the house cannot be stored.</returns>
    internal static bool TryPrepareHome(Wizard wizard, out HouseEntry entry, out HouseGrantFailure failure) {
        entry = null;
        var grant = EnsureStarter(wizard);
        failure = grant.Failure;
        if (!grant.Succeeded) {
            return false;
        }

        var target = ResolveHome(wizard);
        if (target.Item is null || target.Lot == 0) {
            failure = HouseGrantFailure.Unavailable;

            return false;
        }

        var house = HouseRegistry.GetOrCreate(wizard, target.TemplateId, out _);
        if (house is null || !HouseTemplates.TryGet(target.TemplateId, out var template)) {
            Logger.Error("Character {CharId} has no house for template {TemplateId} and none could be stored.",
                Logger.Args(wizard.CharId, target.TemplateId));
            failure = HouseGrantFailure.Unavailable;

            return false;
        }

        entry = HouseEntry.ForDeed(house.LotGid, house.OwnerCharId, template, target.Item.m_globalID, [.. grant.Created.Select(item => (ulong) item.m_globalID)]);

        return true;
    }

    /// <summary>
    /// Points every house item the character holds at its lot, and issues a lot to a house item that has none, so
    /// whatever way the item came, the client reads its lot from it.
    /// </summary>
    /// <param name="wizard">The character.</param>
    internal static void ApplyLots(Wizard wizard) {
        lock (HousingItems.BackpackLock(wizard)) {
            foreach (var item in HouseItems(wizard)) {
                ApplyLot(wizard, item);
            }
        }
    }

    private static void ApplyLot(Wizard wizard, WizClientObjectItem item) {
        if (!TryEnsureLot(wizard, ItemHelper.GetTemplateId(item), out var lot)) {
            Logger.Warning("Could not give house item {ItemId} of character {CharId} a lot.", Logger.Args(item.m_globalID, wizard.CharId));

            return;
        }

        if (!TrySetLot(item, lot, out var changed)) {
            Logger.Warning("House item {ItemId} of character {CharId} has no deed behavior.", Logger.Args(item.m_globalID, wizard.CharId));
        } else if (changed && !WizardItemCollection.ApplyDeedLot(item, lot)) {
            Logger.Warning("Could not store the lot on house item {ItemId} of character {CharId}.", Logger.Args(item.m_globalID, wizard.CharId));
        }
    }

    /// <summary>
    /// Points a house item at a lot.
    /// </summary>
    /// <param name="item">The house item.</param>
    /// <param name="lotGid">The lot.</param>
    /// <param name="changed">Whether the item pointed at another lot before.</param>
    /// <returns><c>false</c> when the item has no deed behavior.</returns>
    internal static bool TrySetLot(WizClientObjectItem item, ulong lotGid, out bool changed) {
        changed = false;
        if (item.m_inactiveBehaviors is null) {
            CoreObjectFactory.InitializeCoreObjectBehaviors(item, item.m_templateID);
        }

        if (!CoreObjectFactory.FindBehaviorInstance<DeedBehavior>(item, out var deed)) {
            return false;
        }

        changed = deed.m_lotInstanceGID != lotGid;
        deed.m_lotInstanceGID = lotGid;

        return true;
    }

    private static HouseGrant IssueAll(Wizard wizard, IReadOnlyList<uint> requested) {
        lock (HousingItems.BackpackLock(wizard)) {
            if (!TryCollectTemplates(requested, out var wanted, out var failure)) {
                return new HouseGrant(failure, []);
            }

            var missing = wanted.Where(template => FindItem(wizard, template.TemplateId) is null).ToList();
            if (missing.Count > wizard.InventoryBehavior.FreeSlots) {
                return new HouseGrant(HouseGrantFailure.BackpackFull, []);
            }

            if (!TryEnsureLots(wizard, wanted, out var lots)) {
                return new HouseGrant(HouseGrantFailure.Unavailable, []);
            }

            var created = new List<WizClientObjectItem>();

            return new HouseGrant(TryIssueAll(wizard, wanted, lots, created) ? HouseGrantFailure.None : HouseGrantFailure.Unavailable, created);
        }
    }

    private static bool TryIssueAll(Wizard wizard, List<HouseTemplateInfo> wanted, Dictionary<uint, ulong> lots, List<WizClientObjectItem> created) {
        foreach (var template in wanted) {
            if (!TryIssue(wizard, template.TemplateId, lots[template.TemplateId], created)) {
                return false;
            }
        }

        return true;
    }

    private static bool TryEnsureLots(Wizard wizard, List<HouseTemplateInfo> wanted, out Dictionary<uint, ulong> lots) {
        lots = [];
        foreach (var template in wanted) {
            if (!TryEnsureLot(wizard, template.TemplateId, out var lot)) {
                Logger.Error("Character {CharId} has no lot for house template {TemplateId} and none could be stored.",
                    Logger.Args(wizard.CharId, template.TemplateId));

                return false;
            }

            lots[template.TemplateId] = lot;
        }

        return true;
    }

    private static bool TryIssue(Wizard wizard, uint templateId, ulong lot, List<WizClientObjectItem> created) {
        var item = FindItem(wizard, templateId);
        if (item is null) {
            item = CreateItem(wizard, templateId, lot);
            if (item is null) {
                return false;
            }

            created.Add(item);

            return true;
        }

        if (!TrySetLot(item, lot, out var changed)) {
            Logger.Error("House item {ItemId} of character {CharId} has no deed behavior.", Logger.Args(item.m_globalID, wizard.CharId));

            return false;
        }

        if (changed && !WizardItemCollection.ApplyDeedLot(item, lot)) {
            Logger.Warning("Could not store the lot on house item {ItemId} of character {CharId}.", Logger.Args(item.m_globalID, wizard.CharId));
        }

        return true;
    }

    private static bool TryCollectTemplates(IReadOnlyList<uint> requested, out List<HouseTemplateInfo> wanted, out HouseGrantFailure failure) {
        wanted = [];
        failure = HouseGrantFailure.None;
        foreach (var templateId in requested) {
            if (!HouseTemplates.TryGet(templateId, out var template)) {
                Logger.Error("Template {TemplateId} is not a house.", Logger.Args(templateId));
                failure = HouseGrantFailure.NotAHouse;

                return false;
            }

            if (wanted.All(known => known.TemplateId != templateId)) {
                wanted.Add(template);
            }

            if (!TryAddStructure(template, wanted)) {
                failure = HouseGrantFailure.Unavailable;

                return false;
            }
        }

        return true;
    }

    private static bool TryAddStructure(HouseTemplateInfo template, List<HouseTemplateInfo> wanted) {
        if (!template.HasStructure || wanted.Any(known => known.TemplateId == template.StructureTemplateId)) {
            return true;
        }

        if (!HouseTemplates.TryGet(template.StructureTemplateId, out var structure)) {
            Logger.Error("The structure {StructureTemplateId} of house template {TemplateId} is not a house.",
                Logger.Args(template.StructureTemplateId, template.TemplateId));

            return false;
        }

        wanted.Add(structure);

        return true;
    }

    private static bool TryEnsureLot(Wizard wizard, uint templateId, out ulong lot) {
        lot = wizard.HouseLots.GetValueOrDefault(templateId);
        if (lot != 0) {
            return true;
        }

        var candidate = (ulong) RandomGen.GenerateGUID();
        if (WizardCollection.TryAddHouseLot(wizard, templateId, candidate)) {
            lot = candidate;

            return true;
        }

        // Another write stored a lot for the template first, so that one is the lot.
        lot = WizardCollection.GetCharacterUnloaded(wizard.CharId)?.HouseLots?.GetValueOrDefault(templateId) ?? 0;
        if (lot != 0) {
            wizard.HouseLots[templateId] = lot;
        }

        return lot != 0;
    }

    private static WizClientObjectItem CreateItem(Wizard wizard, uint templateId, ulong lotGid) {
        var item = HousingItems.CreateItem(wizard, templateId);
        if (item is null) {
            Logger.Error("The house template {TemplateId} is not an item.", Logger.Args(templateId));

            return null;
        }

        if (!TrySetLot(item, lotGid, out _)) {
            Logger.Error("The house template {TemplateId} has no deed behavior.", Logger.Args(templateId));

            return null;
        }

        return wizard.AddInitializedItemToInventory(item) ? item : null;
    }

    private static WizClientObjectItem FindItem(Wizard wizard, uint templateId)
        => HeldItems(wizard).FirstOrDefault(item => ItemHelper.GetTemplateId(item) == templateId);

    private static List<WizClientObjectItem> HouseItems(Wizard wizard)
        => [.. HeldItems(wizard).Where(item => HouseTemplates.TryGet(ItemHelper.GetTemplateId(item), out _))];

    private static IEnumerable<WizClientObjectItem> HeldItems(Wizard wizard)
        => wizard.InventoryBehavior.Items.Concat(wizard.EquipmentBehavior?.EquippedItems ?? []);

}
