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
 * NPC SEAMSTRESS COLLECTION
 * ========================================================================
 *
 * PURPOSE:
 * Looks up which NPC templates offer stitching and unstitching in the SpiralDB data.
 *
 * USAGE EXAMPLE:
 * if (NpcSeamstressCollection.TryGetSeamstress(goTemplate.m_templateID, out _)) { ... }
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class NpcSeamstressCollection {

    /// <summary>
    /// Attempts to retrieve the seamstress record using the template ID of the NPC.
    /// </summary>
    /// <param name="templateID">The template ID of the NPC to be retrieved.</param>
    /// <param name="npcSeamstress">The seamstress record of the NPC.</param>
    /// <returns>True if the NPC offers the seamstress, false otherwise.</returns>
    public static bool TryGetSeamstress(ulong templateID, out NpcSeamstress npcSeamstress)
        => SpiralDB.TryGetNpcSeamstress(templateID, out npcSeamstress);

}
