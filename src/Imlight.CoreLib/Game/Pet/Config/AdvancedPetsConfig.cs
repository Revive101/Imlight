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
 * ADVANCED PET CONFIGURATION
 * ========================================================================
 *
 * PURPOSE:
 * Loads the pet settings of the root archive: the energy regeneration interval and the lowest pet
 * level that can carry a jewel.
 *
 * USAGE EXAMPLE:
 * var interval = AdvancedPetsConfig.EnergyTickInSeconds;
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The pet energy interval and jewel level, as defined in Pets/AdvancedPetsConfig.xml.
/// </summary>
internal sealed class AdvancedPetsConfig : RootSingleResourceSingleton<AdvancedPetsConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "Pets/AdvancedPetsConfig.xml";

    private static Imcodec.ObjectProperty.TypeCache.AdvancedPetsConfig s_config;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<Imcodec.ObjectProperty.TypeCache.AdvancedPetsConfig>(Stream.ToArray(), out var config)) {
            throw new InvalidOperationException("Failed to deserialize Pets/AdvancedPetsConfig.xml.");
        }

        // The energy regeneration code divides by this interval.
        if (config.m_energyTick <= 0) {
            throw new InvalidOperationException($"Pets/AdvancedPetsConfig.xml has the pet energy interval {config.m_energyTick}, it needs a positive one.");
        }

        s_config = config;
        Logger.Information("Loaded the advanced pet configuration, energy interval {Interval} seconds.",
            Logger.Args(config.m_energyTick));

        DisposeStream();
    }

    /// <summary>
    /// The seconds between two pet energy points.
    /// </summary>
    internal static int EnergyTickInSeconds => s_config.m_energyTick;

    /// <summary>
    /// The lowest pet level that can socket a jewel.
    /// </summary>
    internal static int MinPetLevelForJewels => s_config.m_minPetLevelForJewels;

    public void DisposeStream()
        => base.Stream.Dispose();

}
