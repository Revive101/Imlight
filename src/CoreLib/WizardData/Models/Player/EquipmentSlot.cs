/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Implementations;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

public enum EquipmentSlotType {
    Hat,
    Robe,
    Shoes,
    Weapon,
    Athame,
    Amulet,
    Ring,
    Pet,
    Mount,
    Deck
}

/// <summary>
/// Represents a slot in the player's equipment. This is an abstraction on top of Wizard101's
/// type to make it easier to work with.
/// </summary>
[Serializable]
public class EquipmentSlot : IClientTypeProvider<EquippedSlotInfo> {
    public EquipmentSlotType SlotType { get; set; }
    public string ItemName { get; set; }
    public GID ItemId { get; set; }
    public DateTime EquippedSince { get; set; }

    public EquippedSlotInfo GetClientTypeAlternative() {
        // Remove the prefix from the slot type.
        var slotType = SlotType.ToString().Split('.')[^1];
        return new EquippedSlotInfo {
            m_itemID = ItemId,
            m_itemSlotNameID = StringHash.Compute(slotType),
        };
    }
}
