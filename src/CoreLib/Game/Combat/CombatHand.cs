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

    // ctor
    internal CombatHand(List<Spell> spells, byte handSize) {
        _handSize = handSize;
        Spells = spells;
        _exhaustedSpellIds = new List<uint>();
    }

    internal Hand GetHand() {
        // Randomly pick 7 cards from the spellbook, minus the ones we've exhausted.
        var hand = new List<Spell>();
        var random = new Random();
        var _availableCache = AvailableSpells;

        for (var i = 0; i < _handSize; i++) {
            // Spells exhausted!
            if (_availableCache.Count == 0) {
                break;
            }

            var randomIndex = random.Next(0, _availableCache.Count);
            var spell = _availableCache[randomIndex];

            hand.Add(spell);
            _availableCache.Remove(spell);
            _exhaustedSpellIds.Add(spell.m_spellID);
        }

        var handObject = new Hand() {
            m_spellList = new List<Spell>(hand)
        };

        LastGivenHand = hand;

        return handObject;
    }
}
