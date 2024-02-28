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

    private readonly byte _handSize;
    private readonly List<uint> _exhaustedSpellIds;

    // ctor
    internal CombatHand(List<Spell> spells, byte handSize) {
        _handSize = handSize;
        Spells = spells;
        _exhaustedSpellIds = new List<uint>();
    }

    internal List<Spell> GetHand() {
        // Randomly pick 7 cards from the spellbook, minus the ones we've exhausted.
        var hand = new List<Spell>();
        var random = new Random();
        var availableSpells = Spells.Where(spell => !_exhaustedSpellIds.Contains(spell.m_spellID)).ToList();

        for (var i = 0; i < _handSize; i++) {
            // Spells exhausted!
            if (i > availableSpells.Count) {
                break;
            }

            var randomIndex = random.Next(0, availableSpells.Count);
            var spell = availableSpells[randomIndex];

            hand.Add(spell);
            availableSpells.Remove(spell);
            _exhaustedSpellIds.Add(spell.m_spellID);
        }

        return hand;
    }
}
