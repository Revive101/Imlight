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
 * The stored attic of one character: the housing items they put away, and the revision of each blob the client
 * shows them in. Everything in it is a plain domain record, never the client's blob form.
 *
 * USAGE EXAMPLE:
 * var item = new AtticItem { ItemGid = gid, TemplateId = templateId };
 * HousingBlobObject entry = item.ToBlobObject();
 *
 * NOTE:
 * - One document per character, id Attics/{charId}. The container id is the id the client files the attic blobs under.
 * - An item keeps the global id it had as a backpack item, and its item document stays in the items collection, so it comes
 *   back as the same item. Every item is in the one Attic blob. A pet's or mount's Custom is the serialized item behavior its entry carries.
 * - Revisions has one number per blob subtype, the version of that blob. A subtype with no entry has not been sent.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

/// <summary>
/// A housing item in an attic.
/// </summary>
public sealed record AtticItem {

    public uint Id { get; init; }
    public ulong ItemGid { get; init; }
    public uint TemplateId { get; init; }
    public int Tint { get; init; }

    /// <summary>
    /// The serialized item behavior a pet's or mount's entry carries, or <c>null</c> for a plain item.
    /// </summary>
    public byte[] Custom { get; init; }

    /// <summary>
    /// Builds the client's blob entry for the item: the template and the colour, nothing placed.
    /// </summary>
    /// <returns>The entry; a <see cref="HousingBlobCustomObject"/> when the item carries custom data.</returns>
    public HousingBlobObject ToBlobObject() {
        var placed = new PlacedItem { TemplateId = TemplateId, Tint = Math.Clamp(Tint, 0, PlacedItem.MAX_TINT), Custom = Custom }.Normalize();

        return placed?.ToBlobObject() ?? new HousingBlobObject { m_gameObjectTemplateID = TemplateId };
    }

}

/// <summary>
/// The attic of one character.
/// </summary>
public class Attic {

    public string Id { get; set; }
    public ulong CharId { get; set; }
    public ulong ContainerGid { get; set; }
    public Dictionary<string, uint> Revisions { get; set; } = [];
    public uint NextItemId { get; set; } = 1;
    public List<AtticItem> Items { get; set; } = [];
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets the document id of a character's attic.
    /// </summary>
    /// <param name="charId">The character.</param>
    /// <returns>The document id.</returns>
    public static string GetDocumentId(ulong charId)
        => $"Attics/{charId}";

}
