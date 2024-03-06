/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatHand {
    internal List<Spell> Spells;
    internal List<Spell> AvailableSpells => Spells.Where(spell => !_exhaustedSpellIds.Contains(spell.m_spellID)).ToList();
    internal List<Spell> LastGivenHand { get; private set; }

    private readonly byte _handSize;
    private readonly List<uint> _exhaustedSpellIds;
    private readonly List<byte> _discardedCardIndices;

    // ctor
    internal CombatHand(List<Spell> spells, byte handSize) {
        _handSize = handSize;
        Spells = spells;
        _exhaustedSpellIds = new List<uint>();
        LastGivenHand = new List<Spell>();
        _discardedCardIndices = new List<byte>();
    }

    internal Hand GetHand() {
        var newCards = new List<Spell>();
        var random = new Random();
        var _availableCache = AvailableSpells;

        // Remove discarded cards
        for (int i = _discardedCardIndices.Count - 1; i >= 0; i--) {
            var index = _discardedCardIndices[i];
            LastGivenHand.RemoveAt(index); // Remove from hand
            _discardedCardIndices.RemoveAt(i); // Clear tracked index
        }

        // Calculate how many cards need to be refilled
        var cardsToRefill = _handSize - LastGivenHand.Count;

        for (var i = 0; i < cardsToRefill; i++) {
            if (_availableCache.Count == 0) {
                break; // No more spells available
            }

            var randomIndex = random.Next(0, _availableCache.Count);
            var spell = _availableCache[randomIndex];

            newCards.Add(spell);
            _availableCache.Remove(spell);
            _exhaustedSpellIds.Add(spell.m_spellID);
        }

        // Update the LastGivenHand
        LastGivenHand.AddRange(newCards);

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
}

