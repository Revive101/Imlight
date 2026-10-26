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
 * Keeps the wallpaper and flooring of a house. They live in the custom data of one placed object, the texture holder, as
 * the client's own texture remap behavior: a list of remap index and texture item template pairs.
 *
 * USAGE EXAMPLE:
 * if (HouseTextures.TryTarget(message.RemapIndex, textureType, out var target)) {
 *     var changed = HouseTextures.WithTexture(holder, target, templateId, out var replacedTemplateId);
 * }
 *
 * NOTE:
 * - The holder is the object whose template carries a texture remap behavior and is named by Housing.TextureProxyTemplate.
 *   Every house gets one when its state is created, because the client patches a texture list only into an object it has.
 * - A remap index from 1 to 99, with bit 16 for a wall or bit 17 for a floor, is the number of a numbered wall or floor
 *   surface of the house model and applies the texture at once. Without a flag the texture's own type picks wall or floor.
 *   ClientTextureRemapBehavior::ApplyTextureToNamedNodes 0x140f82e50, format_wall_or_floor_name 0x140f83520.
 * - A remap index from 1000 to 1014 or from 2000 to 2014 is a stored slot of the wallpaper and tile window, which only keeps
 *   the texture. SetWallpaperTileWindow::FindFreeTileSlot 0x141079600.
 * - The list is read and written with property mask 0x18 (transmit), as the client does in ClientBlobPatcher::MSG_PatchTextureRemap
 *   0x140f16040.
 *
 * TODO:
 * - MSG_SETTILEWALLPAPER, which picks a stored slot for castle block objects, is not handled.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// Where a texture goes.
/// </summary>
/// <param name="RemapIndex">The remap index as the client sent it.</param>
/// <param name="IsStored">Whether it is a stored slot of the wallpaper and tile window rather than a surface of the house.</param>
/// <param name="IsWall">For a surface, whether it is a wall rather than a floor.</param>
internal readonly record struct TextureTarget(uint RemapIndex, bool IsStored, bool IsWall);

internal static class HouseTextures {

    private const uint WALL_FLAG = 0x10000;
    private const uint FLOOR_FLAG = 0x20000;
    private const uint SURFACE_NUMBER_MASK = 0xFFFF;
    private const uint MAX_SURFACE_NUMBER = 99;
    private const uint FIRST_STORED_TILE_SLOT = 1000;
    private const uint FIRST_STORED_WALLPAPER_SLOT = 2000;
    private const uint STORED_SLOTS_PER_KIND = 15;
    private const int MAX_ENTRIES = 64;
    private const uint TRANSMIT_PROPERTY_MASK = 0x18;

    private static readonly Lazy<uint> s_proxyTemplateId = new(ResolveProxyTemplate);

    /// <summary>
    /// The template of the texture holder, or 0 when textures are off.
    /// </summary>
    internal static uint ProxyTemplateId => s_proxyTemplateId.Value;

    /// <summary>
    /// Checks at boot that the texture holder is set and is a template with a texture remap behavior, and logs when it is not.
    /// </summary>
    /// <returns><c>true</c> when textures are on.</returns>
    internal static bool ValidateProxy()
        => ProxyTemplateId != 0;

    /// <summary>
    /// Checks whether a template is the texture holder.
    /// </summary>
    /// <param name="templateId">An item template id.</param>
    /// <returns><c>true</c> when textures are on and the template is the holder.</returns>
    internal static bool IsProxy(uint templateId)
        => templateId != 0 && templateId == ProxyTemplateId;

    /// <summary>
    /// Creates a texture holder with no textures.
    /// </summary>
    /// <returns>The holder without an id, or <c>null</c> when textures are off or the empty list cannot be written.</returns>
    internal static PlacedItem NewProxy()
        => ProxyTemplateId == 0 ? null : WithEntries(new PlacedItem { TemplateId = ProxyTemplateId }, []);

    /// <summary>
    /// Finds the type of a texture item.
    /// </summary>
    /// <param name="templateId">The item's template id.</param>
    /// <param name="type">Floor or wall when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the template is not a housing texture item.</returns>
    internal static bool TryGetTextureType(uint templateId, out HousingTextureType type) {
        type = default;
        if (CoreObjectFactory.GetCoreTemplate(templateId) is not WizItemTemplate { m_nObjectType: ObjectType.OT_HOUSING_TEXTURE } template) {
            return false;
        }

        var texture = (template.m_behaviors ?? []).OfType<HousingTextureBehaviorTemplate>().FirstOrDefault();
        if (texture is null) {
            return false;
        }

        type = texture.m_textureType;

        return true;
    }

    /// <summary>
    /// Reads where the client wants a texture.
    /// </summary>
    /// <param name="remapIndex">The remap index of the client's request.</param>
    /// <param name="type">The type of the texture item.</param>
    /// <param name="target">The target when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the index is neither a surface number nor a stored slot.</returns>
    internal static bool TryTarget(uint remapIndex, HousingTextureType type, out TextureTarget target) {
        target = default;
        if (IsStoredSlot(remapIndex)) {
            target = new TextureTarget(remapIndex, true, false);

            return true;
        }

        var number = remapIndex & SURFACE_NUMBER_MASK;
        var flags = remapIndex & ~SURFACE_NUMBER_MASK;
        if (number is < 1 or > MAX_SURFACE_NUMBER || flags is not (0 or WALL_FLAG or FLOOR_FLAG)) {
            return false;
        }

        target = new TextureTarget(remapIndex, false, IsWallSurface(remapIndex, type));

        return true;
    }

    /// <summary>
    /// Puts a texture on a surface or in a stored slot.
    /// </summary>
    /// <param name="holder">The texture holder as it is stored.</param>
    /// <param name="target">Where the texture goes.</param>
    /// <param name="templateId">The texture item's template id.</param>
    /// <param name="replacedTemplateId">The template of the texture that was on the surface, or 0 for none.</param>
    /// <returns>The holder with the new list; <c>null</c> when the list is unreadable, full, or the stored slot is taken.</returns>
    internal static PlacedItem WithTexture(PlacedItem holder, TextureTarget target, uint templateId, out uint replacedTemplateId) {
        replacedTemplateId = 0;
        if (!TryRead(holder, out var entries)) {
            return null;
        }

        if (target.IsStored && entries.Any(entry => entry.m_remapIndex == target.RemapIndex)) {
            return null;
        }

        var replaced = target.IsStored ? null : entries.FirstOrDefault(entry => OccupiesSurface(entry, target));
        if (replaced is not null) {
            entries.Remove(replaced);
            replacedTemplateId = replaced.m_textureTemplateID;
        }

        if (entries.Count >= MAX_ENTRIES) {
            return null;
        }

        entries.Add(new TextureRemap { m_remapIndex = target.RemapIndex, m_textureTemplateID = templateId });

        return WithEntries(holder, entries);
    }

    /// <summary>
    /// Takes a texture out of the list.
    /// </summary>
    /// <param name="holder">The texture holder as it is stored.</param>
    /// <param name="removed">The entry to take out, as <see cref="TryFind"/> found it.</param>
    /// <returns>The holder with the new list; <c>null</c> when the entry is no longer in the list.</returns>
    internal static PlacedItem Without(PlacedItem holder, TextureRemap removed) {
        if (!TryRead(holder, out var entries)) {
            return null;
        }

        var index = entries.FindIndex(entry => entry.m_remapIndex == removed.m_remapIndex && entry.m_textureTemplateID == removed.m_textureTemplateID);
        if (index < 0) {
            return null;
        }

        entries.RemoveAt(index);

        return WithEntries(holder, entries);
    }

    /// <summary>
    /// Finds the entry a pick up request names.
    /// </summary>
    /// <param name="holder">The texture holder as it is stored.</param>
    /// <param name="remapIndex">The remap index of the request: a stored slot, or the number of a surface.</param>
    /// <param name="textureType">The type of the texture the client wants back: 1 for a wall, 0 for a floor.</param>
    /// <param name="found">The entry when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the list holds no such texture.</returns>
    internal static bool TryFind(PlacedItem holder, uint remapIndex, uint textureType, out TextureRemap found) {
        found = null;
        if (!TryRead(holder, out var entries)) {
            return false;
        }

        found = entries.FirstOrDefault(entry => IsNamedBy(entry, remapIndex, textureType == (uint) HousingTextureType.HTT_WALL));

        return found is not null;
    }

    /// <summary>
    /// Gets the type value the client expects with a removed texture.
    /// </summary>
    /// <param name="entry">The removed entry.</param>
    /// <returns>1 for a wall, 0 for a floor or an entry whose template is not a texture.</returns>
    internal static uint TextureTypeOf(TextureRemap entry)
        => TryGetTextureType(entry.m_textureTemplateID, out var type) ? (uint) type : 0;

    private static uint ResolveProxyTemplate() {
        var configured = HousingSettings.TextureProxyTemplate;
        if (configured == 0) {
            Logger.Warning("Housing.TextureProxyTemplate is not set, so wallpaper and flooring cannot be applied.");

            return 0;
        }

        if (CoreObjectFactory.GetCoreTemplate(configured) is not WizItemTemplate template
            || !(template.m_behaviors ?? []).OfType<TextureRemapBehaviorTemplate>().Any()) {
            Logger.Error("Housing.TextureProxyTemplate names template {TemplateId}, which has no texture remap behavior.", Logger.Args(configured));

            return 0;
        }

        return configured;
    }

    private static bool IsStoredSlot(uint remapIndex)
        => remapIndex - FIRST_STORED_TILE_SLOT < STORED_SLOTS_PER_KIND || remapIndex - FIRST_STORED_WALLPAPER_SLOT < STORED_SLOTS_PER_KIND;

    private static bool IsApplied(TextureRemap entry)
        => entry.m_remapIndex <= (SURFACE_NUMBER_MASK | WALL_FLAG | FLOOR_FLAG)
           && (entry.m_remapIndex & SURFACE_NUMBER_MASK) is >= 1 and < FIRST_STORED_TILE_SLOT;

    private static bool IsWallSurface(uint remapIndex, HousingTextureType type)
        => (remapIndex & FLOOR_FLAG) == 0 && ((remapIndex & WALL_FLAG) != 0 || type == HousingTextureType.HTT_WALL);

    private static bool OccupiesSurface(TextureRemap entry, TextureTarget target)
        => IsApplied(entry)
           && (entry.m_remapIndex & SURFACE_NUMBER_MASK) == (target.RemapIndex & SURFACE_NUMBER_MASK)
           && TryGetTextureType(entry.m_textureTemplateID, out var type)
           && IsWallSurface(entry.m_remapIndex, type) == target.IsWall;

    private static bool IsNamedBy(TextureRemap entry, uint remapIndex, bool wantsWall) {
        if (!IsApplied(entry)) {
            return entry.m_remapIndex == remapIndex;
        }

        return (entry.m_remapIndex & SURFACE_NUMBER_MASK) == (remapIndex & SURFACE_NUMBER_MASK)
               && TryGetTextureType(entry.m_textureTemplateID, out var type)
               && IsWallSurface(entry.m_remapIndex, type) == wantsWall;
    }

    private static bool TryRead(PlacedItem holder, out List<TextureRemap> entries) {
        entries = [];
        if (!HousingBlobs.TryDeserialize<ClientTextureRemapBehavior>(holder?.Custom, TRANSMIT_PROPERTY_MASK, out var behavior)) {
            return false;
        }

        entries = [.. (behavior.m_textureRemapList ?? []).Where(entry => entry is not null)];

        return true;
    }

    private static PlacedItem WithEntries(PlacedItem holder, List<TextureRemap> entries) {
        try {
            var custom = (byte[]) HousingBlobs.Serialize(new ClientTextureRemapBehavior { m_textureRemapList = entries }, TRANSMIT_PROPERTY_MASK);

            return (holder with { Custom = custom }).Normalize();
        } catch (InvalidOperationException exception) {
            Logger.Warning("Could not write the textures of a house: {Reason}", Logger.Args(exception.Message));

            return null;
        }
    }

}
