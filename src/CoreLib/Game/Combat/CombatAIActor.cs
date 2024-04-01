/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

/*
This script is responsible for determining the behavior of AI-controlled creatures in combat.
It is developed in stages of complexity, named after the in-game rankings.

Novice     -- Randomly selects a spell from its hand and casts it.
Journeyman -- Details intelligence, aggressiveness, and selfishness factors.
*/

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatAIActor : ReceiveProtocolDispatcher {
    private const float HEALING_THRESHOLD = 0.75f;
    private const float HEALING_PERCENT_CHANCE = 0.5f;
    private const float PREPARE_PASS_CHANCE = 0.05f;

    private readonly IActorRef _creatureActorRef;
    private readonly CombatDuelActor _duelActor;
    private readonly CombatDuelActorSubCircle _mySubcircle;
    private readonly WizGameStats _stats;
    private readonly MagicSchool _magicSchool;
    private readonly int _level;
    private readonly Random _random = new();
    private readonly Dictionary<int, int> _hateTable = new();

    // The chance that the creature will use a spell that is the most damaging spell in its hand.
    private readonly float _intelligenceFactor;
    private bool _determinedSmartThisTurn;

    // The chance that the creature will attack the enemy team. If not, it will "prepare." (blade/heal/shield/trap/etc.)
    private readonly float _aggressivenessFactor;
    private bool _determinedAggressiveThisTurn;

    // The chance that when a creature is preparing, it will buff itself rather than a teammate.
    private readonly float _selfishnessFactor;
    private bool _determinedSelfishThisTurn;

    private Hand _roundHand;
    private CombatDuelActorSubCircle[] _friendlySubcircles
        => _duelActor.ActiveSubCircles.Where(x => x.OccupiedTeam == _mySubcircle.OccupiedTeam).ToArray();
    private bool _isHealingViable
        => _friendlySubcircles.Any(x => x.ParticipantGameStats.m_currentHitpoints / x.ParticipantGameStats.m_baseHitpoints < HEALING_THRESHOLD);

    // ctor
    public CombatAIActor(IActorRef creatureActor, CombatDuelActor duelActor, CombatDuelActorSubCircle mySubcircle) {
        this._creatureActorRef = creatureActor;
        this._duelActor = duelActor;
        this._mySubcircle = mySubcircle;

        // Query the creature actor for the creature's stats
        var rsp = _creatureActorRef
            .Ask<COMBAT_106_PROTOCOL.MSG_CREATURESTATS>(new COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS())
            .Result;
        this._stats = rsp.GameStats;
        this._intelligenceFactor = rsp.CombatIntelligence;
        this._selfishnessFactor = rsp.CombatSelfishFactor;
        this._aggressivenessFactor = rsp.CombatAggressionFactor;
        this._magicSchool = rsp.MagicSchool;
        this._level = rsp.CombatLevel;

        InitiatizeHateTable();
    }

    // Akka.NET ctor
    public static Props Props(IActorRef creatureActor, CombatDuelActor duelActor, CombatDuelActorSubCircle mySubcircle)
        => Akka.Actor.Props.Create(() => new CombatAIActor(creatureActor, duelActor, mySubcircle));

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewCombatRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        _roundHand = _mySubcircle.DrawHand();

        DetermineAttitude();
        var action = DetermineTurnAction();

        // Send the action to the duel actor.
        _duelActor.ActorRef.Tell(action);
    }

    private void DetermineAttitude() {
        _determinedSmartThisTurn = _random.NextDouble() < _intelligenceFactor;
        _determinedAggressiveThisTurn = _random.NextDouble() < _aggressivenessFactor;
        _determinedSelfishThisTurn = _random.NextDouble() < _selfishnessFactor;

        Logger.Debug("Duel {0} | Slot {1} | Smart: {2} | Aggressive: {3} | Selfish: {4}",
            Logger.Args(_duelActor.SigilId,
                        _mySubcircle.SlotIndex,
                        _determinedSmartThisTurn,
                        _determinedAggressiveThisTurn,
                        _determinedSelfishThisTurn));
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineTurnAction() {
        if (_determinedAggressiveThisTurn) {
            return DetermineAggressiveBehavior();
        }
        else {
            return DetermineDefensiveBehavior();
        }
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineAggressiveBehavior() {
        // We want to cast a spell.
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
            Actor = _creatureActorRef,
            MoveType = (byte) CombatMoveType.Attack,
            SpellSelection = 0,
            SpellTarget = 0,
        };

        var castableDamageSpells = GetCastableDamageSpells(_roundHand.m_spellList);

        // If we have no castable damage spells, we'll just pass.
        if (castableDamageSpells.Count == 0) {
            Logger.Debug("Duel {0} | Slot {1} | No castable damage spells.",
                Logger.Args(_duelActor.SigilId, _mySubcircle.SlotIndex));

            msg.MoveType = (byte) CombatMoveType.Pass;
            return msg;
        }

        var targetIdx = GetMostHatedTarget();
        msg.SpellTarget = (byte) targetIdx;

        // Are we smart enough to use our highest pip spell?
        if (_determinedSmartThisTurn) {
            var highestPipSpell = GetHighestPipSpell(castableDamageSpells);
            if (highestPipSpell is not null) {
                msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(highestPipSpell);
                return msg;
            }
        }

        // Otherwise, choose a random spell.
        var randomIdx = _random.Next(castableDamageSpells.Count);
        var selectedSpell = castableDamageSpells[randomIdx];
        msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(selectedSpell);

        return msg;
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineDefensiveBehavior() {
        // We want to prepare.
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
            Actor = _creatureActorRef,
            MoveType = (byte) CombatMoveType.Attack,
            SpellSelection = 0,
            SpellTarget = 0,
        };

        // There's a very low chance that we just pass on a prepare turn.
        if (_random.NextDouble() < PREPARE_PASS_CHANCE) {
            Logger.Debug("Duel {0} | Slot {1} | Preparing, but passing.",
                Logger.Args(_duelActor.SigilId, _mySubcircle.SlotIndex));

            msg.MoveType = (byte) CombatMoveType.Pass;
            return msg;
        }

        if (_isHealingViable && _random.NextDouble() < HEALING_PERCENT_CHANCE) {
            var healingSpells = GetCastableHealingSpells(_roundHand.m_spellList);
            if (healingSpells.Count > 0) {
                return DetermineHealingBehavior();
            }
        }

        var buffSpells = GetCastableBuffSpells(_roundHand.m_spellList);
        var debuffSpells = GetCastableDebuffSpells(_roundHand.m_spellList);
        if (buffSpells.Count == 0 && debuffSpells.Count == 0) {
            // If we have no buff or debuff spells, we'll just pass.
            msg.MoveType = (byte) CombatMoveType.Pass;
        }

        return msg;
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineHealingBehavior() {
        // We've determined that we want to heal. Either us or a teammate.
        var castableHealingSpells = GetCastableHealingSpells(_roundHand.m_spellList);
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
            Actor = _creatureActorRef,
            MoveType = (byte) CombatMoveType.Attack,
            SpellSelection = 0,
            SpellTarget = 0,
        };

        // Determine if we're smart enough to use the highest pip healing spell.
        if (_determinedSmartThisTurn) {
            var highestPipHealingSpell = GetHighestPipSpell(castableHealingSpells);
            if (highestPipHealingSpell is not null) {
                msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(highestPipHealingSpell);
            }
        }

        // Otherwise, choose a random healing spell.
        var randomIdx = _random.Next(castableHealingSpells.Count);
        var selectedSpell = castableHealingSpells[randomIdx];
        msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(selectedSpell);

        // If we're below the healing threshold, and selfish enough, heal ourselves.
        var selfThresholdMatched = _stats.m_currentHitpoints / _stats.m_baseHitpoints < HEALING_THRESHOLD;
        if (selfThresholdMatched && _determinedSelfishThisTurn) {
            msg.SpellTarget = (uint) _mySubcircle.SlotIndex;
        }
        else {
            // Otherwise, heal the teammate with the lowest health percentage.
            var lowestHealthTeammate = _friendlySubcircles
                .OrderBy(x => x.ParticipantGameStats.m_currentHitpoints / x.ParticipantGameStats.m_baseHitpoints)
                .First();

            msg.SpellTarget = (byte) (lowestHealthTeammate.SlotIndex + 1);
        }

        return msg;
    }

    private void DiscardSpells(List<Spell> spells) {
        foreach (var spell in spells) {
            _mySubcircle.DiscardCard(spell);
        }
    }

    private List<Spell> GetCastableSpells(List<Spell> spells) {
        var castableSpells = new List<Spell>();
        foreach (var spell in spells) {
            if (!_mySubcircle.HasPipsForSpell(spell)) {
                continue;
            }

            castableSpells.Add(spell);
        }

        return castableSpells;
    }

    private List<Spell> GetCastableDamageSpells(List<Spell> spells) {
        var castableSpells = GetCastableSpells(spells);
        return SpellEffectFilter.FilterSpellsByOutgoingDamage(castableSpells);
    }

    private List<Spell> GetCastableHealingSpells(List<Spell> spells) {
        var castableSpells = GetCastableSpells(spells);
        return SpellEffectFilter.FilterSpellsByHealing(castableSpells);
    }

    private List<Spell> GetCastableBuffSpells(List<Spell> spells) {
        var castableSpells = GetCastableSpells(spells);
        return SpellEffectFilter.FilterSpellsByBuff(castableSpells);
    }

    private List<Spell> GetCastableDebuffSpells(List<Spell> spells) {
        var castableSpells = GetCastableSpells(spells);
        return SpellEffectFilter.FilterSpellsByDebuff(castableSpells);
    }

    private Spell GetHighestPipSpell(List<Spell> spells) {
        if (_roundHand is null || _roundHand.m_spellList.Count == 0) {
            return null;
        }

        // Order the spells by pip cost, descending.
        // Then iterate through the list and return the first spell that we have pips for.
        var orderedSpells = _roundHand.m_spellList.OrderByDescending(x => x.m_pipCost.m_spellRank);
        foreach (var spell in orderedSpells) {
            if (!_mySubcircle.HasPipsForSpell(spell)) {
                continue;
            }

            return spell;
        }

        return null;
    }

    private void InitiatizeHateTable() {
        // Create the hate table. There is one for each slot in the duel.
        for (int i = 0; i < _duelActor.SubCircles.Count(); i++) {
            _hateTable.Add(i, 0);
        }

        // The initial target for this creature will be the slot across from it, or the first slot alive.
        var myIdx = _mySubcircle.SlotIndex;
        var target = _duelActor.ActiveSubCircles.FirstOrDefault(x => x.SlotIndex > 3 && x.IsAlive);
        if (target is null) {
            return;
        }

        var targetIdx = target.SlotIndex;
        UpdateHateTable(targetIdx, 1);
    }

    private int GetMostHatedTarget() {
        var maxHate = _hateTable.Values.Max();
        var mostHatedTarget = _hateTable.FirstOrDefault(x => x.Value == maxHate && x.Key >= 4).Key;
        if (mostHatedTarget < 4) {
            mostHatedTarget = _duelActor.ActiveSubCircles
                .FirstOrDefault(x => x.SlotIndex >= 4 && x.IsAlive)?.SlotIndex ?? mostHatedTarget;
        }
        return mostHatedTarget;
    }

    private void UpdateHateTable(int targetIdx, int hateValue) {
        // Make sure we aren't targeting ourselves.
        if (targetIdx == _mySubcircle.SlotIndex) {
            Logger.Error("Creature tried to target itself.");
            return;
        }

        _hateTable[targetIdx] += hateValue;
    }
}
