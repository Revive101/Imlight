/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class CombatJourneymanAIComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const int FINAL_KILL_DELAY_IN_MS = 2000;

    public ITimerScheduler Timers { get; set; }

    private readonly float _healingThreshold = ConfigurationManager.Settings.HealingThreshold;
    private readonly float _healingPercentChance = ConfigurationManager.Settings.HealingPercentChance;
    private readonly float _preparePassChance = ConfigurationManager.Settings.PreparePassChance;
    private readonly int _damagedAggroIncrease = ConfigurationManager.Settings.DamagedAggroIncrease;
    private readonly int _healingAggroIncrease = ConfigurationManager.Settings.HealingAggroIncrease;
    private readonly int _provokeAggroIncrease = ConfigurationManager.Settings.ProvokeAggroIncrease;
    private readonly int _pacifyAggroDecrease = ConfigurationManager.Settings.PacifyAggroDecrease;
    private readonly Dictionary<int, int> _hateTable = [];
    private readonly Random _random = new();

    private PathMovementComponent _pathMovementComponent;
    private NpcComponent _npcComponent;
    private StatsComponent _stats;
    private CombatDuelComponent _currentDuelComponent;
    private CombatDuelSubCircle _currentSubCircle;

    // The chance that the creature will use a spell that is the most damaging spell in its hand.
    private float _intelligenceFactor;
    private bool _determinedSmartThisTurn;

    // The chance that the creature will attack the enemy team. If not, it will "prepare." (blade/heal/shield/trap/etc.)
    private float _aggressivenessFactor;
    private bool _determinedAggressiveThisTurn;

    // The chance that when a creature is preparing, it will buff itself rather than a teammate.
    private float _selfishnessFactor;
    private bool _determinedSelfishThisTurn;

    private Hand _roundHand;
    private CombatDuelSubCircle[] _friendlySubcircles
        => _currentDuelComponent.ActiveSubCircles.Where(x => x.OccupiedTeam == _currentSubCircle.OccupiedTeam).ToArray();
    private bool _isHealingViable
        => _friendlySubcircles.Any(x => x.ParticipantGameStats.m_currentHitpoints / x.ParticipantGameStats.m_baseHitpoints < _healingThreshold);
    private bool _sentFinalKill;
    private bool _isInDuel;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && gameObjectTemplate.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    public override void OnStart() {
        _pathMovementComponent = Entity.GetComponentOfType<PathMovementComponent>();
        if (_pathMovementComponent is null) {
            Logger.Warning(
                "{0} requires component of type {1} to be attached to the entity. Found on GameObject {2}",
                Logger.Args(
                    nameof(CombatJourneymanAIComponent),
                    nameof(PathMovementComponent),
                    Entity.ActiveGameObject.m_debugName
                )
            );

            return;
        }

        _npcComponent = Entity.GetComponentOfType<NpcComponent>();
        if (_npcComponent is null) {
            Logger.Warning(
                "{0} requires component of type {1} to be attached to the entity. Found on GameObject {2}",
                Logger.Args(
                    nameof(CombatJourneymanAIComponent),
                    nameof(NpcComponent),
                    Entity.ActiveGameObject.m_debugName
                )
            );

            return;
        }

        _intelligenceFactor = _npcComponent.IntelligenceFactor;
        _aggressivenessFactor = _npcComponent.AggressiveFactor;
        _selfishnessFactor = _npcComponent.SelfishnessFactor;

        _stats = Entity.GetComponentOfType<StatsComponent>();
        if (_stats is null) {
            Logger.Warning(
                "{0} requires component of type {1} to be attached to the entity. Found on GameObject {2}",
                Logger.Args(
                    nameof(CombatJourneymanAIComponent),
                    nameof(StatsComponent),
                    Entity.ActiveGameObject.m_debugName
                )
            );

            return;
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void ReceiveCombatAdded(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        if (_sentFinalKill) {
            return;
        }

        _isInDuel = true;
        _currentDuelComponent = message.Duel;
        _currentSubCircle = message.SubCircle;
        _pathMovementComponent.Stop();
        InitiatizeHateTable();
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewCombatRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        if (_sentFinalKill) {
            return;
        }

        _roundHand = _currentSubCircle.DrawHand();

        DetermineAttitude();
        var action = DetermineTurnAction();

        // Send the action to the duel actor.
        _currentDuelComponent.ActorRef.Tell(action, Self);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATEFFECT))]
    private void ReceiveCombatEffect(COMBAT_106_PROTOCOL.MSG_COMBATEFFECT message) {
        if (_sentFinalKill) {
            return;
        }

        // Determine if I am included in the target array.
        var isTarget = message.Targets.Any(x => x.SlotIndex == _currentSubCircle.SlotIndex);
        var isHealing = message.Effect.m_effectType is SpellEffect.kSpellEffects.kHeal
                                                    or SpellEffect.kSpellEffects.kHealOverTime
                                                    or SpellEffect.kSpellEffects.kHealPercent;

        // Ignore if the caster is on my team.
        var isOnMyTeam = message.Caster.OccupiedTeam == _currentSubCircle.OccupiedTeam;
        if (isOnMyTeam) {
            return;
        }

        if (isTarget) {
            var isPacify = message.Effect.m_effectType is SpellEffect.kSpellEffects.kPacify;
            var isProvoke = message.Effect.m_effectType is SpellEffect.kSpellEffects.kTaunt;

            int hateValue;
            if (isPacify) {
                hateValue = -_pacifyAggroDecrease;
            }
            else if (isProvoke) {
                hateValue = _provokeAggroIncrease;
            }
            else {
                hateValue = _damagedAggroIncrease;
            }

            UpdateHateTable(message.Caster.SlotIndex, hateValue);
        }
        else if (isHealing) {
            UpdateHateTable(message.Caster.SlotIndex, _healingAggroIncrease);
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATDEATH))]
    private void ReceiveCombatDeath(COMBAT_106_PROTOCOL.MSG_COMBATDEATH message) {
        if (_sentFinalKill || !_isInDuel) {
            Entity.DeleteObject();

            return;
        }

        // I have died. Remove myself from the current duel.
        _currentDuelComponent.DuelBroadcast(new WIZARDCOMBAT_51_PROTOCOL.MSG_COMBATREMOVE {
            DuelID = _currentDuelComponent.SigilId,
            ParticipantID = _currentSubCircle.ParticipantObject.m_globalID
        });

        // Send this message to ourselves again after a delay to delete the entity. This lets the combat death animation play out,
        // and then the entity will be deleted.
        _sentFinalKill = true;
        var delay = TimeSpan.FromMilliseconds(FINAL_KILL_DELAY_IN_MS);
        Timers.StartSingleTimer("FinalKill", new COMBAT_106_PROTOCOL.MSG_COMBATDEATH(), delay);
    }

    private void DetermineAttitude() {
        _determinedSmartThisTurn = _random.NextDouble() < _intelligenceFactor;
        _determinedAggressiveThisTurn = _random.NextDouble() < _aggressivenessFactor;
        _determinedSelfishThisTurn = _random.NextDouble() < _selfishnessFactor;

        Logger.Debug("Duel {0} | Slot {1} | Smart: {2} | Aggressive: {3} | Selfish: {4}",
            Logger.Args(_currentDuelComponent.SigilId,
                _currentSubCircle.SlotIndex,
                _determinedSmartThisTurn,
                _determinedAggressiveThisTurn,
                _determinedSelfishThisTurn));
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineTurnAction() {
        // If I'm stunned, pass.
        if (_currentSubCircle.CombatParticipant.m_stunned > 0) {
            _currentSubCircle.CombatParticipant.m_stunned--;

            Logger.Debug("Duel {0} | Slot {1} | Stunned. Passing.",
                Logger.Args(_currentDuelComponent.SigilId, _currentSubCircle.SlotIndex));

            return new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
                Actor = Entity.SelfRef,
                MoveType = (byte) CombatMoveType.Pass,
                SpellSelection = 0,
                SpellTarget = 0,
            };
        }

        // If we want to be aggressive and we have something to cast, do it.
        var hasDamageSpells = GetCastableDamageSpells(_roundHand.m_spellList).Count > 0;
        if (_determinedAggressiveThisTurn && hasDamageSpells) {
            return DetermineAggressiveBehavior();
        }
        else {
            return DetermineDefensiveBehavior();
        }
    }

    private COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE DetermineAggressiveBehavior() {
        // We want to cast a spell.
        var msg = new COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE {
            Actor = Entity.SelfRef,
            MoveType = (byte) CombatMoveType.Attack,
            SpellSelection = 0,
            SpellTarget = 0,
        };

        var castableDamageSpells = GetCastableDamageSpells(_roundHand.m_spellList);

        // If we have no castable damage spells, we'll just pass.
        if (castableDamageSpells.Count == 0) {
            Logger.Debug("Duel {0} | Slot {1} | No castable damage spells.",
                Logger.Args(_currentDuelComponent.SigilId, _currentSubCircle.SlotIndex));

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
            Actor = Entity.SelfRef,
            MoveType = (byte) CombatMoveType.Attack,
            SpellSelection = 0,
            SpellTarget = 0,
        };

        // There's a very low chance that we just pass on a prepare turn.
        if (_random.NextDouble() < _preparePassChance) {
            Logger.Debug("Duel {0} | Slot {1} | Preparing, but passing.",
                Logger.Args(_currentDuelComponent.SigilId, _currentSubCircle.SlotIndex));

            msg.MoveType = (byte) CombatMoveType.Pass;
            return msg;
        }

        if (_isHealingViable && _random.NextDouble() < _healingPercentChance) {
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
                msg.SpellTarget = (uint) _currentSubCircle.SlotIndex;
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
            Actor = Entity.SelfRef,
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
        var selfThresholdMatched = _stats.Stats.m_currentHitpoints / _stats.Stats.m_baseHitpoints < _healingThreshold;
        if (selfThresholdMatched && _determinedSelfishThisTurn) {
            msg.SpellTarget = (uint) _currentSubCircle.SlotIndex;
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
            if (!_currentSubCircle.HasPipsForSpell(spell)) {
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
        int start = (_currentSubCircle.SlotIndex < 3) ? 4 : 0;
        int end = (_currentSubCircle.SlotIndex < 3) ? _currentDuelComponent.SubCircles.Length : _currentDuelComponent.SubCircles.Length / 2;

        for (int i = start; i < end; i++) {
            _hateTable.Add(i, 0);
        }

        // Our initial target will be whomever is across from us.
        var targetIdx = _currentSubCircle.SlotIndex + 4 % _currentDuelComponent.SubCircles.Length;
        UpdateHateTable(targetIdx, 1);
    }

    private int GetMostHatedTarget() {
        var orderedHateTable = _hateTable.OrderByDescending(x => x.Value);

        // Pick the highest hated target that is still alive.
        foreach (var (targetIdx, hateValue) in orderedHateTable) {
            var target = _currentDuelComponent.SubCircles[targetIdx];
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
        if (targetIdx == _currentSubCircle.SlotIndex) {
            Logger.Error("Creature tried to target itself.");
            return;
        }

        _hateTable[targetIdx] += hateValue;
    }

}