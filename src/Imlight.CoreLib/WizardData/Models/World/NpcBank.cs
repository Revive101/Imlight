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
 * NPC BANK
 * ========================================================================
 *
 * PURPOSE:
 * The SpiralDB record of an NPC that offers the bank.
 *
 * USAGE EXAMPLE:
 * SpiralDB.TryGetNpcBank(templateId, out _) is true for a bank NPC template.
 *
 * NOTE:
 * The record is a mark: an NPC offers the bank when SpiralDB holds a record for its template. The template
 * data carries no bank marker of its own.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

namespace Imlight.CoreLib.WizardData.Models.World;

public class NpcBank {

    /// <summary>
    /// The NPC template ID that offers the bank.
    /// </summary>
    public ulong TemplateID { get; set; }

}
