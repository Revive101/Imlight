/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Game.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatDeck {
    internal List<Spell> LastGivenHand { get; private set; }
    internal int TotalCardCount => (int) _spellData.Sum(s => s.m_quantity);
    internal int RemainingCardCount => (int) _usedUpSpellData.Sum(s => s.m_quantity);

    private readonly List<SpellData> _spellData;
    private readonly byte _handSize;
    private readonly List<SpellData> _usedUpSpellData;
    private readonly List<byte> _discardedCardIndices;

    // ctor
    internal CombatDeck(List<SpellData> spellDatas, byte handSize) {
        this._spellData = spellDatas;
        this._handSize = handSize;
        this.LastGivenHand = new List<Spell>();
        this._discardedCardIndices = new List<byte>();

        // Clone the spell data into used up spell data.
        _usedUpSpellData = new List<SpellData>(_spellData);
    }

    internal Hand GetHand() {
        var newCards = new List<Spell>();
        var random = new Random();

        // Calculate how many cards need to be refilled
        var cardsToRefill = _handSize - LastGivenHand.Count;

        // Decrement the quantity of the spells that were discarded
        foreach (var index in _discardedCardIndices) {
            if (_usedUpSpellData[index].m_quantity > 0) {
                _usedUpSpellData[index].m_quantity--;
            }

            // If the quantity of the spell is 0, remove it from the used up spell data
            if (_usedUpSpellData[index].m_quantity <= 0) {
                _usedUpSpellData.RemoveAt(index);
            }
        }

        for (var i = 0; i < cardsToRefill; i++) {
            if (RemainingCardCount < 0) {
                break; // No more spells available
            }

            var randomIndex = random.Next(0, _usedUpSpellData.Count);
            var spellTemplateId = _usedUpSpellData[randomIndex].m_templateID;

            // Create a new spell from the template id
            var spell = SpellFactory.CreateSpellFromTemplate(spellTemplateId)
                ?? throw new InvalidOperationException("Spell could not be created from template id.");
            newCards.Add(spell);

            // Decrement the quantity of the spell
            _usedUpSpellData[randomIndex].m_quantity--;
        }

        // Update the LastGivenHand
        LastGivenHand = newCards;

        return new Hand() {
            m_spellList = new List<Spell>(LastGivenHand) // Return the full hand
        };
    }

    internal void Discard(byte index) {
        if (index >= 0 && index < LastGivenHand.Count) {
            _discardedCardIndices.Add(index);
        }
        else {
            throw new ArgumentOutOfRangeException(nameof(index), "Index out of range.");
        }
    }

    internal void Discard(Spell spell) {
        var index = LastGivenHand.FindIndex(s => s.m_spellID == spell.m_spellID);
        if (index != -1) {
            _discardedCardIndices.Add((byte) index);
        }
        else {
            throw new ArgumentException("Spell not found in hand.", nameof(spell));
        }
    }
}

