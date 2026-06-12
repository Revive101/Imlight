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

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Contains metadata about a spell card in a combat deck.
/// </summary>
/// <remarks>
/// Tracks the template ID, quantity, and special properties of spell cards,
/// distinguishing between regular, item, and battle cards.
/// </remarks>
internal class CombatDeckSpellData {
    
    public uint TemplateId { get; set; }
    public uint Quantity { get; set; }
    public bool IsBattleCard { get; set; }
    public bool IsItemCard { get; set; }

}
