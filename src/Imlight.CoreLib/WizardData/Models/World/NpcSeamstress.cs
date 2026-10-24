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
 * NPC SEAMSTRESS
 * ========================================================================
 *
 * PURPOSE:
 * The SpiralDB record of an NPC that offers stitching and unstitching.
 *
 * USAGE EXAMPLE:
 * NpcSeamstressCollection.TryGetSeamstress(templateId, out _) is true for a seamstress NPC template.
 *
 * NOTE:
 * The record is a mark: an NPC offers the seamstress when SpiralDB holds a record for its template. It is kept
 * apart from NpcInventory because a seamstress can have no shop inventory in SpiralDB.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

namespace Imlight.CoreLib.WizardData.Models.World;

public class NpcSeamstress {

    /// <summary>
    /// The NPC template ID that offers stitching and unstitching.
    /// </summary>
    public ulong TemplateID { get; set; }

}
