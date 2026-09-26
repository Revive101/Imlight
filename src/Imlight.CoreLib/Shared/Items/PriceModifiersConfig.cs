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
 * PRICE MODIFIERS CONFIG
 * ========================================================================
 *
 * PURPOSE:
 * Loads PriceModifiers.xml from the Root.wad so the dye shop charges what the
 * client's dye shop window shows for a dye or a pet rename.
 *
 * USAGE EXAMPLE:
 * var dyeCost = PriceModifiersConfig.GetDyeCost(template, message.texture, message.decal);
 * var renameCost = PriceModifiersConfig.GetPetRenameCost();
 *
 * NOTE:
 * The costs mirror the client's DyeShopWindow price display, float math and rounding included.
 * Without the file, the client's own defaults apply: no floor, x1 multipliers, a free rename.
 *
 * TODO:
 * - The client also scales a dye by a per-swatch modifier (m_shoppingColors, by button and gender).
 *   Every shipped value is 1, and how a swatch button maps to a dye value is unknown, so it is left out.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Shared.Items;

internal class PriceModifiersConfig : RootSingleResourceSingleton<PriceModifiersConfig>, IMemoryStreamDisposable {

    // The client rounds a fraction of 0.498 or more up, not 0.5.
    private const float RoundUpFraction = 0.498f;

    protected override string ResourceName => "PriceModifiers.xml";

    private static DyeShopModifiers s_dyeShopModifiers;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var deserialized = serializer.Deserialize<PriceModifiers>(Stream.ToArray(), out var priceModifiers);
        DisposeStream();

        if (!deserialized) {
            Logger.Error("Could not deserialize {0} as {1}",
                Logger.Args(ResourceName, nameof(PriceModifiers)));

            return;
        }

        s_dyeShopModifiers = priceModifiers.m_dyeShopMods;

        Logger.Information("Loaded dye shop prices: dye at {0} of the item cost plus {1}, pet rename {2} gold.",
            Logger.Args(s_dyeShopModifiers?.m_multiplierTemplateCost, s_dyeShopModifiers?.m_costFloorAdditive,
                s_dyeShopModifiers?.m_petRenameCost));
    }

    internal static int GetPetRenameCost() => s_dyeShopModifiers?.m_petRenameCost ?? 0;

    internal static int GetDyeCost(WizItemTemplate template, int primaryDye, int secondaryDye) {
        // The pattern never changes the price.
        var cost = template.m_baseCost
            * (s_dyeShopModifiers?.m_multiplierTemplateCost ?? 1f)
            * GetColorListMultiplier(primaryDye, template.m_numPrimaryColors)
            * GetColorListMultiplier(secondaryDye, template.m_numSecondaryColors);

        return (s_dyeShopModifiers?.m_costFloorAdditive ?? 0) + RoundUp(cost);
    }

    private static float GetColorListMultiplier(int dye, int templateColorCount) {
        // A template with a single color has no colors of its own, so every dye is an extra one.
        var ownColorCount = templateColorCount == 1 ? 0 : templateColorCount;

        return dye < ownColorCount
            ? s_dyeShopModifiers?.m_multiplierIfDropList ?? 1f
            : s_dyeShopModifiers?.m_multiplierIfDyeList ?? 1f;
    }

    private static int RoundUp(float cost) {
        var whole = (int) cost;

        return cost - whole < RoundUpFraction ? whole : whole + 1;
    }

    public void DisposeStream() => Stream.Dispose();

}
