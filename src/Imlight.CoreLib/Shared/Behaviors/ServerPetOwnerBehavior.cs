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
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetOwnerBehavior : IClientBehaviorProvider<ClientPetOwnerBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public readonly int EnergyTickIntervalInSeconds = ConfigurationManager.Settings["Character.PetEnergyTickInSeconds"].AsInt();

    public byte MaxSlots { get; set; }
    public List<CraftingSlot> MorphingSlots { get; set; }
    public uint LastEnergyTickEpoch { get; private set; }
    public int Energy { get; private set; }
    public bool PlayingAsPet { get; set; }

    public void SetEnergy(int energy) {
        Energy = energy;
        LastEnergyTickEpoch = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EnergyTickIntervalInSeconds);
    }

    public ClientPetOwnerBehavior GetClientBehaviorInstance() => new() {
        // For `m_energyTickTimeSecs`, it doesn't matter what value we set here.
        // In PetService.cs after attachment, the client will receive the correct value.
        m_maxSlots = MaxSlots,
        m_morphingSlots = MorphingSlots,
        m_energyTickTimeSecs = 0,
        m_energy = Energy,
        m_playingAsPet = PlayingAsPet
    };
    
}
