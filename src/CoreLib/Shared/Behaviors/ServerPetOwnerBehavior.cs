/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common.Configuration;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetOwnerBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    public readonly int EnergyTickIntervalInSeconds = ConfigurationManager.Settings.PetEnergyTickInSeconds;

    public byte MaxSlots { get; set; }
    public List<CraftingSlot> MorphingSlots { get; set; }
    public uint LastEnergyTickEpoch { get; private set; }
    public int Energy { get; private set; }
    public bool PlayingAsPet { get; set; }

    public void SetEnergy(int energy) {
        Energy = energy;
        LastEnergyTickEpoch = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EnergyTickIntervalInSeconds);
    }

    public override ClientPetOwnerBehavior GetClientBehaviorInstance() => new() {
        // For `m_energyTickTimeSecs`, it doesn't matter what value we set here.
        // In PetService.cs after attachment, the client will receive the correct value.
        m_maxSlots = MaxSlots,
        m_morphingSlots = MorphingSlots,
        m_energyTickTimeSecs = 0,
        m_energy = Energy,
        m_playingAsPet = PlayingAsPet
    };
}
