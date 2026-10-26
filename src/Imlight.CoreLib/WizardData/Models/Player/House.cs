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
 * The stored state of one player house: its lot, the template it was issued from, its revision and the
 * placed items. Everything in it is a plain domain record, never the client's blob form.
 * 
 * USAGE EXAMPLE:
 * var id = House.GetDocumentId(wizard.HouseLots[templateId]);
 * 
 * NOTE:
 * - A lot is one issued deed or structure item. Its global id is the document id, so a character who owns
 *   several houses has one document per lot.
 * - Revisions has one number per live blob subtype, the version the client's blob cache compares. Every stored change moves
 *   the revision of the blob it touches forward. A subtype with no entry has not been sent. An old document's single revision is ignored.
 * - Item ids are never reused: NextItemId is the id of the next item placed.
 * - Decorators are the character ids of the friends the owner lets move the objects of the house.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.Player;

/// <summary>
/// A house owned by one character.
/// </summary>
public class House {

    public string Id { get; set; }
    public ulong LotGid { get; set; }
    public ulong OwnerCharId { get; set; }
    public uint TemplateId { get; set; }
    public Dictionary<string, uint> Revisions { get; set; } = [];
    public int BonusSlots { get; set; }
    public uint NextItemId { get; set; } = 1;
    public List<PlacedItem> Items { get; set; } = [];
    public List<ulong> Decorators { get; set; } = [];
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets the document id of a house.
    /// </summary>
    /// <param name="lotGid">The lot of the house.</param>
    /// <returns>The document id.</returns>
    public static string GetDocumentId(ulong lotGid)
        => $"Houses/{lotGid}";

}
