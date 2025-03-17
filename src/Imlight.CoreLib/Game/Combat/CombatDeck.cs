/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatDeck {

    internal List<Spell> LastGivenHand { get; private set; }
    internal int TotalCardCount => (int) _spellData.Sum(s => s.Quantity);
    internal int RemainingCardCount => (int) _usedUpSpellData.Sum(s => s.Quantity);

    private readonly List<CombatDeckSpellData> _spellData;
    private readonly byte _handSize;
    private readonly List<CombatDeckSpellData> _usedUpSpellData;
    private readonly List<Spell> _cardsDiscardedThisTurn;

    // ctor
    internal CombatDeck(List<CombatDeckSpellData> spellDatas, byte handSize) {
        this._spellData = spellDatas;
        this._handSize = handSize;
        this.LastGivenHand = [];
        this._cardsDiscardedThisTurn = [];

        // Clone the spell data into used up spell data.
        this._usedUpSpellData = [];
        foreach (var originalSpellData in _spellData) {
            _usedUpSpellData.Add(new CombatDeckSpellData() {
                TemplateId = originalSpellData.TemplateId,
                Quantity = originalSpellData.Quantity,
                IsBattleCard = originalSpellData.IsBattleCard,
                IsItemCard = originalSpellData.IsItemCard
            });
        }
    }

    internal Hand GetHand() {
        var newCards = new List<Spell>();
        var random = new Random();

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

            var randomIndex = random.Next(0, _usedUpSpellData.Count);
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

    internal void Discard(Spell spell) => _cardsDiscardedThisTurn.Add(spell);

    internal void Reshuffle() {
        // Copy spell data back to used up spell data.
        _usedUpSpellData.Clear();
        foreach (var originalSpellData in _spellData) {
            _usedUpSpellData.Add(new CombatDeckSpellData() {
                TemplateId = originalSpellData.TemplateId,
                Quantity = originalSpellData.Quantity,
                IsBattleCard = originalSpellData.IsBattleCard,
                IsItemCard = originalSpellData.IsItemCard
            });
        }

        _cardsDiscardedThisTurn.Clear();
    }
    
}

