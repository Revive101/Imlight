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
using System.Linq;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.Shared.Utilities;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

public record PetEggData {
    public ulong GlobalId { get; set; }
    public ulong PetTemplateId { get; set; }
    public int HatchTimeEpoch { get; set; }
}

public record PetEggData {
    public ulong GlobalId { get; set; }
    public ulong PetTemplateId { get; set; }
    public int HatchTimeEpoch { get; set; }
}

public class ServerPetOwnerBehavior : IClientBehaviorProvider<ClientPetOwnerBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public readonly int EnergyTickIntervalInSeconds = ConfigurationManager
        .Settings["Character.PetEnergyTickInSeconds"].AsInt();
    public readonly int EnergyTickIntervalInSeconds = ConfigurationManager
        .Settings["Character.PetEnergyTickInSeconds"].AsInt();

    public byte MaxSlots { get; set; }
    public uint LastEnergyTickEpoch { get; private set; }
    public int Energy { get; private set; }
    public bool PlayingAsPet { get; set; }

    /// <summary>
    /// Persisted egg data. Rebuilt into _runtimeEggs on load.
    /// </summary>
    public List<PetEggData> Eggs { get; set; }

    /// <summary>
    /// Runtime-only CraftingSlot list rebuilt from Eggs on load.
    /// ClientPetOwnerBehavior.m_morphingSlots is NOT populated from this —
    /// the client learns about eggs via MSG_PETEGGMORPHED/MSG_PETMORPHINGSLOT messages.
    /// </summary>
    [JsonIgnore] public List<CraftingSlot> MorphingSlots { get; private set; }

    /// <summary>
    /// The template ID of the currently equipped pet (0 if none).
    /// </summary>
    public ulong EquippedPetTemplateId { get; set; }

    /// <summary>
    /// The inventory item GlobalID of the currently equipped pet (0 if none).
    /// </summary>
    public ulong EquippedPetGlobalId { get; set; }

    // ctor
    public ServerPetOwnerBehavior() 
        => MorphingSlots = [];

    /// <summary>
    /// Rebuilds the runtime MorphingSlots from persisted Eggs after database load.
    /// </summary>
    public void RebuildRuntimeSlots() {
        MorphingSlots = [];
        if (Eggs == null) {
            return;
        }

        foreach (var eggData in Eggs) {
            MorphingSlots.Add(new CraftingSlot {
                m_globalID = eggData.GlobalId,
                m_templateID = 0, // Never populated.
                m_recipeName = $"PetEgg:{eggData.PetTemplateId}",
                m_timeFinished = eggData.HatchTimeEpoch
            });
        }
    }

    public void SetEnergy(int energy) {
        Energy = energy;
        LastEnergyTickEpoch = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + EnergyTickIntervalInSeconds);
    }

    /// <summary>
    /// Creates a pet egg and adds it to both the persisted Eggs list and runtime MorphingSlots.
    /// </summary>
    /// <param name="petTemplateId">The template ID of the pet this egg will hatch into.</param>
    /// <param name="hatchTimeSeconds">Seconds from now until the egg hatches.</param>
    /// <param name="globalId">Optional: use this GlobalID instead of generating one.
    /// Should match the inventory item's m_globalID so MSG_HATCHEGGNOW can find the egg.</param>
    /// <returns>The created CraftingSlot (egg) for sending messages.</returns>
    public CraftingSlot CreatePetEgg(ulong petTemplateId, uint hatchTimeSeconds, ulong globalId = 0) {
        Eggs ??= [];
        MorphingSlots ??= [];

        var hatchEpoch = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + hatchTimeSeconds);
        if (globalId == 0) {
            globalId = RandomGen.GenerateGUID();
        }

        // Persist as simple DTO.
        Eggs.Add(new PetEggData {
            GlobalId = globalId,
            PetTemplateId = petTemplateId,
            HatchTimeEpoch = hatchEpoch
        });

        // Runtime CraftingSlot for timer logic (NOT sent to client in behavior blob).
        var egg = new CraftingSlot {
            m_globalID = globalId,
            m_templateID = 0,
            m_recipeName = $"PetEgg:{petTemplateId}",
            m_timeFinished = hatchEpoch
        };
        MorphingSlots.Add(egg);

        return egg;
    }

    /// <summary>
    /// Extracts the pet template ID from an egg's recipe name.
    /// </summary>
    public static bool TryGetPetTemplateFromEgg(CraftingSlot egg, out ulong petTemplateId) {
        petTemplateId = 0;
        if (egg?.m_recipeName == null) {
            return false;
        }

        if (!egg.m_recipeName.StartsWith("PetEgg:")) {
            return false;
        }

        return ulong.TryParse(egg.m_recipeName.AsSpan(7), out petTemplateId);
    }

    /// <summary>
    /// Hatches an egg: removes from both the persisted Eggs list and runtime MorphingSlots.
    /// </summary>
    public bool HatchEgg(CraftingSlot egg) {
        if (MorphingSlots == null) {
            return false;
        }

        if (!MorphingSlots.Remove(egg)) {
            return false;
        }

        // Also remove from persisted list.
        Eggs?.RemoveAll(e => e.GlobalId == egg.m_globalID);

        return true;
    }

    /// <summary>
    /// Returns all eggs whose hatch time has passed.
    /// </summary>
    public List<CraftingSlot> GetReadyEggs() {
        if (MorphingSlots == null) {
            return [];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return MorphingSlots.Where(e => e.m_timeFinished <= now).ToList();
    }

    /// <summary>
    /// Returns all pending eggs (hatch time still in the future).
    /// </summary>
    public List<CraftingSlot> GetPendingEggs() {
        if (MorphingSlots == null) {
            return [];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return [.. MorphingSlots.Where(e => e.m_timeFinished > now)];
    }

    /// <summary>
    /// Marks a pet as equipped, storing its template and item IDs.
    /// </summary>
    public void EquipPet(WizItemTemplate template, WizClientObjectItem item) {
        EquippedPetTemplateId = item.m_templateID;
        EquippedPetGlobalId = item.m_globalID;
        PlayingAsPet = true;
    }

    /// <summary>
    /// Marks the pet as unequipped.
    /// </summary>
    public void UnequipPet() {
        EquippedPetTemplateId = 0;
        EquippedPetGlobalId = 0;
        PlayingAsPet = false;
    }

    /// <summary>
    /// Returns the client-facing behavior. m_morphingSlots is always set to null —
    /// the client learns about eggs via MSG_PETEGGMORPHED / MSG_PETMORPHINGSLOT
    /// network messages, not from the serialized behavior blob.
    /// </summary>
    public ClientPetOwnerBehavior GetClientBehaviorInstance() => new() {
        m_maxSlots = MaxSlots,
        m_morphingSlots = null,
        m_morphingSlots = null,
        m_energyTickTimeSecs = 0,
        m_energy = Energy,
        m_playingAsPet = PlayingAsPet
    };
    
}
