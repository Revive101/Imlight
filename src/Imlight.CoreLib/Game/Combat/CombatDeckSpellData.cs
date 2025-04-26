/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
