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
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.WizardData.Implementations;

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
