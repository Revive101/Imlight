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
 * COMBAT SPELL DECK MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the drawing, discarding, and tracking of spell cards during combat,
 * providing randomized card selection from available spells.
 * 
 * USAGE EXAMPLE:
 * var deck = new CombatDeck(spellDatas, handSize);
 * Hand newHand = deck.GetHand();
 * deck.Discard(spell);
 * 
 * NOTE:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Manages a participant's spell deck during combat, including drawing cards and handling discards.
/// </summary>
internal class CombatDeck {

    internal List<Spell> LastGivenHand { get; private set; }
    internal int TotalCardCount => (int) _spellData.Sum(s => s.Quantity);
    internal int RemainingCardCount => (int) _usedUpSpellData.Sum(s => s.Quantity);
    internal int VaultTotalCount => (int) _treasureVault.Sum(s => s.Quantity);
    internal int VaultRemainingCount => (int) _treasureVaultUsed.Sum(s => s.Quantity);
    internal int TreasureCardsInHand { get; private set; }

    private readonly List<CombatDeckSpellData> _spellData;
    private readonly List<CombatDeckSpellData> _treasureVault;
    private readonly byte _handSize;
    private readonly List<CombatDeckSpellData> _usedUpSpellData;
    private readonly List<CombatDeckSpellData> _treasureVaultUsed;
    private readonly List<Spell> _cardsDiscardedThisTurn;

    // ctor
    internal CombatDeck(List<CombatDeckSpellData> spellDatas, List<CombatDeckSpellData> treasureVault, byte handSize) {
        this._spellData = spellDatas;
        this._treasureVault = treasureVault ?? [];
        this._handSize = handSize;
        this.LastGivenHand = [];
        this._cardsDiscardedThisTurn = [];
        this.TreasureCardsInHand = 0;

        // Clone the spell data into used up spell data.
        this._usedUpSpellData = [];
        foreach (var originalSpellData in _spellData) {
            _usedUpSpellData.Add(new CombatDeckSpellData() {
                TemplateId = originalSpellData.TemplateId,
                Quantity = originalSpellData.Quantity,
                IsBattleCard = originalSpellData.IsBattleCard,
                IsItemCard = originalSpellData.IsItemCard,
                IsTreasureCard = false
            });
        }

        // Clone the treasure vault.
        this._treasureVaultUsed = [];
        foreach (var vaultCard in _treasureVault) {
            _treasureVaultUsed.Add(new CombatDeckSpellData() {
                TemplateId = vaultCard.TemplateId,
                Quantity = vaultCard.Quantity,
                IsBattleCard = vaultCard.IsBattleCard,
                IsItemCard = vaultCard.IsItemCard,
                IsTreasureCard = true
            });
        }
    }

    /// <summary>
    /// Gets a new hand of spells, discarding any used or discarded cards.
    /// </summary>
    /// <returns>A new hand of spells.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a spell cannot be created from the template id.</exception>
    internal Hand GetHand() {
        var newCards = new List<Spell>();
        // Discard the cards that were used up or discarded.
        foreach (var spell in _cardsDiscardedThisTurn) {
            LastGivenHand.Remove(spell);

            var spellData = _usedUpSpellData.FirstOrDefault(s => s.TemplateId == spell.m_templateID);
            if (spellData == null) {
                // The spell may not be in this list if the previous hand used them all.
                continue;
            }

            // Decrement the quantity of the spell, or remove it if the quantity is 0.
            if (spellData.Quantity - 1 <= 0) {
                _usedUpSpellData.Remove(spellData);
            }
            else {
                spellData.Quantity--;
            }
        }
        _cardsDiscardedThisTurn.Clear();

        // Refill the hand with new cards.
        var cardsToRefill = _handSize - LastGivenHand.Count;
        for (var i = 0; i < cardsToRefill; i++) {
            if (RemainingCardCount <= 0) {
                break; // No more spells available.
            }

            var randomIndex = Random.Shared.Next(0, _usedUpSpellData.Count);
            var spellData = _usedUpSpellData[randomIndex];
            var spellTemplateId = spellData.TemplateId;

            // Create a new spell from the template id
            var spell = SpellFactory.GetSpell(spellTemplateId)
                ?? throw new InvalidOperationException("Spell could not be created from template id.");
            spell.m_itemCard = spellData.IsItemCard;
            spell.m_battleCard = spellData.IsBattleCard;

            newCards.Add(spell);

            // Decrement the quantity of the spell, or remove it if the quantity is 0.
            if (spellData.Quantity - 1 <= 0) {
                _usedUpSpellData.RemoveAt(randomIndex);
            }
            else {
                spellData.Quantity--;
            }
        }

        // Update the LastGivenHand.
        LastGivenHand.AddRange(newCards);

        return new Hand() { m_spellList = LastGivenHand };
    }

    /// <summary>
    /// Discards a spell from the current hand. Treasure cards are returned to the vault.
    /// </summary>
    /// <param name="spell">The spell to discard.</param>
    internal void Discard(Spell spell) {
        if (spell.m_treasureCard) {
            ReturnToVault(spell);
        }
        else {
            _cardsDiscardedThisTurn.Add(spell);
        }
    }

    /// <summary>
    /// Draws a random treasure card from the vault and adds it to the current hand.
    /// </summary>
    /// <returns>The drawn spell, or null if the vault is empty or hand is full.</returns>
    internal Spell DrawFromVault() {
        if (VaultRemainingCount <= 0) {
            return null;
        }

        if (LastGivenHand.Count >= _handSize) {
            return null;
        }

        // Player must have more total cards available than hand size to draw from vault.
        // This prevents drawing from the sideboard when the regular deck still has cards.
        var totalAvailableCards = RemainingCardCount + VaultRemainingCount + LastGivenHand.Count;
        if (totalAvailableCards <= _handSize) {
            return null;
        }

        var randomIndex = Random.Shared.Next(0, _treasureVaultUsed.Count);
        var vaultData = _treasureVaultUsed[randomIndex];

        var spell = SpellFactory.GetSpell(vaultData.TemplateId);
        if (spell == null) {
            return null;
        }

        spell.m_treasureCard = true;
        LastGivenHand.Add(spell);
        TreasureCardsInHand++;

        // Decrement or remove from vault.
        if (vaultData.Quantity - 1 <= 0) {
            _treasureVaultUsed.RemoveAt(randomIndex);
        }
        else {
            vaultData.Quantity--;
        }

        return spell;
    }

    /// <summary>
    /// Returns a treasure card to the vault (e.g., on discard or fizzle).
    /// </summary>
    internal void ReturnToVault(Spell spell) {
        if (!spell.m_treasureCard) {
            return;
        }

        // Remove from hand.
        LastGivenHand.Remove(spell);
        if (TreasureCardsInHand > 0) {
            TreasureCardsInHand--;
        }

        // Return to vault used list.
        var existing = _treasureVaultUsed.Find(v => v.TemplateId == spell.m_templateID);
        if (existing != null) {
            existing.Quantity++;
        }
        else {
            _treasureVaultUsed.Add(new CombatDeckSpellData {
                TemplateId = spell.m_templateID,
                Quantity = 1,
                IsTreasureCard = true
            });
        }
    }

    /// <summary>
    /// Permanently consumes a successfully cast treasure card from the vault.
    /// Returns the template ID so the caller can persist the removal.
    /// </summary>
    internal uint ConsumeFromVault(Spell spell) {
        if (!spell.m_treasureCard) {
            return 0;
        }

        // Remove from hand.
        LastGivenHand.Remove(spell);
        if (TreasureCardsInHand > 0) {
            TreasureCardsInHand--;
        }

        // Remove from the persistent vault list (not the used copy — this removes it permanently).
        var vaultEntry = _treasureVault.Find(v => v.TemplateId == spell.m_templateID);
        if (vaultEntry != null) {
            if (vaultEntry.Quantity - 1 <= 0) {
                _treasureVault.Remove(vaultEntry);
            }
            else {
                vaultEntry.Quantity--;
            }
        }

        return spell.m_templateID;
    }

    /// <summary>
    /// Reshuffles the deck, resetting the used up spell data and clearing the discarded cards.
    /// Vault cards are NOT returned to the deck on reshuffle.
    /// </summary>
    internal void Reshuffle() {
        // Copy spell data back to used up spell data.
        _usedUpSpellData.Clear();
        foreach (var originalSpellData in _spellData) {
            _usedUpSpellData.Add(new CombatDeckSpellData() {
                TemplateId = originalSpellData.TemplateId,
                Quantity = originalSpellData.Quantity,
                IsBattleCard = originalSpellData.IsBattleCard,
                IsItemCard = originalSpellData.IsItemCard,
                IsTreasureCard = false
            });
        }

        _cardsDiscardedThisTurn.Clear();
    }
    
}

