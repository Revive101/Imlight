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
    private const float PREPARE_PASS_CHANCE = 0.33f;
    private const int DAMAGED_AGGRO_INCREASE = 5;
    private const int HEALING_AGGRO_INCREASE = 3;
    private const int PROVOKE_AGGRO_INCREASE = 20;
    private const int PACIFY_AGGRO_DECREASE = 50;

    private readonly IActorRef _creatureActorRef;
    private readonly CombatDuelActor _duelActor;
    private readonly CombatDuelActorSubCircle _mySubcircle;
    private readonly ServerWizGameStats _stats;
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

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATEFFECT))]
    private void ReceiveCombatEffect(COMBAT_106_PROTOCOL.MSG_COMBATEFFECT message) {
        // Determine if I am included in the target array.
        var isTarget = message.Targets.Any(x => x.SlotIndex == _mySubcircle.SlotIndex);
        var isHealing = message.Effect.m_effectType is SpellEffect.kSpellEffects.kHeal
                                                    or SpellEffect.kSpellEffects.kHealOverTime
                                                    or SpellEffect.kSpellEffects.kHealPercent;

        // Ignore if the caster is on my team.
        var isOnMyTeam = message.Caster.OccupiedTeam == _mySubcircle.OccupiedTeam;
        if (isOnMyTeam) {
            return;
        }

        if (isTarget) {
            var isPacify = message.Effect.m_effectType is SpellEffect.kSpellEffects.kPacify;
            var isProvoke = message.Effect.m_effectType is SpellEffect.kSpellEffects.kTaunt;

            int hateValue;
            if (isPacify) {
                hateValue = -PACIFY_AGGRO_DECREASE;
            }
            else if (isProvoke) {
                hateValue = PROVOKE_AGGRO_INCREASE;
            }
            else {
                hateValue = DAMAGED_AGGRO_INCREASE;
            }

            UpdateHateTable(message.Caster.SlotIndex, hateValue);
        }
        else if (isHealing) {
            UpdateHateTable(message.Caster.SlotIndex, HEALING_AGGRO_INCREASE);
        }
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
        // If I'm stunned, pass.
        if (_mySubcircle.CombatParticipant.m_stunned > 0) {
            _mySubcircle.CombatParticipant.m_stunned--;

            Logger.Debug("Duel {0} | Slot {1} | Stunned. Passing.",
                Logger.Args(_duelActor.SigilId, _mySubcircle.SlotIndex));

            return new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
                Actor = _creatureActorRef,
                MoveType = (byte) CombatMoveType.Pass,
                SpellSelection = 0,
                SpellTarget = 0,
            };
        }

        // If we want to be aggressive and we have something to cast, do it.
        if (_determinedAggressiveThisTurn && GetCastableDamageSpells(_roundHand.m_spellList).Count > 0) {
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
            var highestPipSpell = castableDamageSpells
                .OrderByDescending(x => x.m_pipCost.m_spellRank)
                .FirstOrDefault();
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
            return msg;
        }

        // Flip a coin to either cast a buff or a debuff.
        var coinFlip = _random.NextDouble();
        if (coinFlip < 0.5 && buffSpells.Count > 0) {
            // cast a buff.
            var randomIdx = _random.Next(buffSpells.Count);
            var selectedSpell = buffSpells[randomIdx];
            msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(selectedSpell);

            // Are we selfish? If so, cast it on ourselves. Otherwise, cast it on a teammate.
            if (_determinedSelfishThisTurn) {
                msg.SpellTarget = (uint) _mySubcircle.SlotIndex;
            }
            else {
                // Otherwise, select a random teammate.
                var randomTeammate = _friendlySubcircles[_random.Next(_friendlySubcircles.Length)];
                msg.SpellTarget = (byte) (randomTeammate.SlotIndex + 1);
            }
        }
        else if (debuffSpells.Count > 0) {
            // cast a debuff.
            var randomIdx = _random.Next(debuffSpells.Count);
            var selectedSpell = debuffSpells[randomIdx];
            msg.SpellSelection = (byte) _roundHand.m_spellList.IndexOf(selectedSpell);

            // Select our most hated enemy.
            var targetIdx = GetMostHatedTarget();
            msg.SpellTarget = (byte) targetIdx;
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
            var highestPipHealingSpell = castableHealingSpells
                .OrderByDescending(x => x.m_pipCost.m_spellRank)
                .FirstOrDefault();
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

    private void InitiatizeHateTable() {
        // Create the hate table.
        int start = (_mySubcircle.SlotIndex < 3) ? 4 : 0;
        int end = (_mySubcircle.SlotIndex < 3) ? _duelActor.SubCircles.Length : _duelActor.SubCircles.Length / 2;

        for (int i = start; i < end; i++) {
            _hateTable.Add(i, 0);
        }

        // Our initial target will be whomever is across from us.
        var targetIdx = _mySubcircle.SlotIndex + 4 % _duelActor.SubCircles.Length;
        UpdateHateTable(targetIdx, 1);
    }

    private int GetMostHatedTarget() {
        var orderedHateTable = _hateTable.OrderByDescending(x => x.Value);

        // Pick the highest hated target that is still alive.
        foreach (var (targetIdx, hateValue) in orderedHateTable) {
            var target = _duelActor.SubCircles[targetIdx];
            if (target is null || !target.Occupied) {
                continue;
            }

            if (target.IsAlive) {
                return targetIdx;
            }
        }

        return 0;
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
