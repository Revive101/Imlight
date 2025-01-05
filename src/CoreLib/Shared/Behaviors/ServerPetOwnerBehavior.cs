/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common.Configuration;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetOwnerBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    public byte MaxSlots { get; set; }
    public List<CraftingSlot> MorphingSlots { get; set; }
    public uint NextEnergyTickEpoch { get; private set; }
    public int Energy { get; private set; }
    public bool PlayingAsPet { get; set; }

    public void SetEnergy(int energy) {
        Energy = energy;

        // Next energy tick is now + 7.5 minutes.
        var currentTick = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        NextEnergyTickEpoch = (uint) (currentTick + 450);
    }

    public override ClientPetOwnerBehavior GetClientBehaviorInstance() => new() {
        m_maxSlots = MaxSlots,
        m_morphingSlots = MorphingSlots,
        m_energyTickTimeSecs = NextEnergyTickEpoch,
        m_energy = Energy,
        m_playingAsPet = PlayingAsPet
    };
}
