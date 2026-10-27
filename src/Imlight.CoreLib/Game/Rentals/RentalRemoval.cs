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
 * RENTAL REMOVAL
 * ========================================================================
 *
 * PURPOSE:
 *
 * USAGE EXAMPLE:
 *
 * NOTE:
 * SlotType and SlotIndex only mean something when WasEquipped is true. RemovedEffects is never null.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Rentals;

internal sealed record RentalRemoval(bool WasEquipped,
                                     EquipmentSlotType SlotType,
                                     byte SlotIndex,
                                     List<GameEffectBase> RemovedEffects,
                                     ulong TemplateId) {

    private const byte NO_SLOT = 255;

    internal string SlotName => SlotType.ToString();

    internal static RentalRemoval FromBackpack(WizClientObjectItem item)
        => new(false, EquipmentSlotType.Mount, NO_SLOT, [], item.m_templateID.Full);

}
