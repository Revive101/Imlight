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
 * Finds, creates and shares the live state of every house in use. All players in a house get
 * the same HouseState.
 * 
 * USAGE EXAMPLE:
 * var state = HouseRegistry.GetOrCreate(wizard, templateId, out var created);
 * if (HouseRegistry.TryEnter(lotGid, out var house)) { ... HouseRegistry.Release(house); }
 * 
 * NOTE:
 * - Zone loads never create houses; only an owner's Home click does.
 * - A house is stored under its lot. The character's lot for a template is in Wizard.HouseLots, and a new
 *   house is stored together with that pointer in one transaction.
 * - A state leaves the registry when its last viewer leaves. It is already persisted by then.
 * - Whether a character may enter a house is decided by the caller, not here.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// The houses that are loaded, by lot.
/// </summary>
internal static class HouseRegistry {

    private static readonly ConcurrentDictionary<ulong, HouseState> s_houses = new();
    private static readonly StripedLocks s_creationLocks = new();

    /// <summary>
    /// Finds a house by its lot, loading it from the database when it is not live.
    /// </summary>
    /// <param name="lotGid">The lot to find.</param>
    /// <returns>The live house, or <c>null</c> when no house is stored under the lot or it cannot be loaded.</returns>
    internal static HouseState Get(ulong lotGid) => Publish(lotGid);

    /// <summary>
    /// Finds a character's house of a template, creating and storing it when the character has none. However many
    /// logins race, one document and one lot exist per character and template.
    /// </summary>
    /// <param name="owner">The owning character.</param>
    /// <param name="templateId">The deed or structure template of the house.</param>
    /// <param name="created">Whether this call created the house.</param>
    /// <returns>The live house, or <c>null</c> when the template is not a house or the house cannot be loaded or stored.</returns>
    internal static HouseState GetOrCreate(Wizard owner, uint templateId, out bool created) {
        created = false;
        if (!HouseTemplates.TryGet(templateId, out var template)) {
            Logger.Error("Template {TemplateId} is not a house.", Logger.Args(templateId));

            return null;
        }

        var lotGid = owner.HouseLots.GetValueOrDefault(templateId);
        var house = lotGid == 0 ? null : HouseCollection.Load(lotGid);
        if (house is null) {
            var layout = HouseLayouts.Resolve(template, template.LotZone);
            if (layout is null) {
                return null;
            }

            var items = layout
                .Select((item, index) => item with { Id = (uint) index + 1 })
                .ToList();
            lotGid = lotGid == 0 ? (ulong) RandomGen.GenerateGUID() : lotGid;
            var candidate = new House {
                Id = House.GetDocumentId(lotGid),
                LotGid = lotGid,
                OwnerCharId = owner.CharId,
                TemplateId = templateId,
                BonusSlots = 0,
                NextItemId = (uint) items.Count + 1,
                Items = items,
                CreatedUtc = DateTime.UtcNow,
            };
            created = HouseCollection.TryCreate(candidate, owner, out house);
            if (house is null) {
                return null;
            }

            if (created) {
                Logger.Information("Created house {LotGid} of template {TemplateId} for character {CharId}",
                    Logger.Args(house.LotGid, templateId, owner.CharId));
            }
        }

        if (house.OwnerCharId != owner.CharId || house.TemplateId != templateId) {
            Logger.Error("House {LotGid} is not the house of template {TemplateId} of character {CharId}.",
                Logger.Args(house.LotGid, templateId, owner.CharId));

            return null;
        }

        owner.HouseLots[templateId] = house.LotGid;

        return Publish(house.LotGid);
    }

    /// <summary>
    /// Finds a house by its lot and counts a viewer in.
    /// </summary>
    /// <param name="lotGid">The lot to find.</param>
    /// <param name="state">The live house when this returns <c>true</c>; call <see cref="Release"/> when the viewer is gone.</param>
    /// <returns><c>false</c> when no house is stored under the lot.</returns>
    internal static bool TryEnter(ulong lotGid, out HouseState state) {
        while ((state = Get(lotGid)) is not null) {
            if (state.Enter()) {
                return true;
            }

            Evict(state);
        }

        return false;
    }

    /// <summary>
    /// Counts a viewer out of a house and drops the house from the registry when nobody is left.
    /// </summary>
    /// <param name="state">The house the viewer entered with <see cref="TryEnter"/>.</param>
    internal static void Release(HouseState state) {
        if (state.Leave()) {
            Evict(state);
        }
    }

    /// <summary>
    /// Drops a state from the registry, unless the registry already holds another state for its lot.
    /// </summary>
    /// <param name="state">The state to drop.</param>
    internal static void Evict(HouseState state)
        => s_houses.TryRemove(new KeyValuePair<ulong, HouseState>(state.LotGid, state));

    private static HouseState Publish(ulong lotGid) {
        while (true) {
            if (s_houses.TryGetValue(lotGid, out var existing)) {
                if (!existing.IsRetired) {
                    return existing;
                }

                Evict(existing);

                continue;
            }

            // The house is loaded under the lock, after any earlier state has stored its last change.
            lock (s_creationLocks.For(lotGid)) {
                if (s_houses.ContainsKey(lotGid)) {
                    continue;
                }

                var house = HouseCollection.Load(lotGid);
                if (house is null || !HouseTemplates.TryGet(house.TemplateId, out var template)
                    || !HouseState.TryCreate(house, template, out var created)) {
                    return null;
                }

                s_houses[lotGid] = created;

                return created;
            }
        }
    }

}
