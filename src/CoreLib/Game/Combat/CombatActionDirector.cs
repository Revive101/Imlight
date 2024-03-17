/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlgiht.CoreLib.Game.Spells;
using Imlight.Common;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

public class QueuedCombatAction {
    public CombatDuelActorSubCircle SpellCaster;
    public CombatDuelActorSubCircle TargetSubcircle;
    public Spell Spell;
    public bool PredeterminedSuccess;
}

public class CombatActionDirector {
    private const int SPELL_ACTION_TIME = 10;
    private const int SPELL_FIZZLE_TIME = 4;
    private const int SPELL_PASS_TIME = 1;
    private const float SPELL_CAST_TIME = 4.0f;
    private const float DAMAGE_OVER_TIME_CINEMATIC_TIME = 2.0f;

    private readonly Duel _duel;
    private readonly CombatEffectApplicator _effects;

    private readonly CombatDuelActorSubCircle[] _subCircles = new CombatDuelActorSubCircle[8];
    private CombatDuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();
    private List<QueuedCombatAction> _queuedCombatActions;

    // ctor
    public CombatActionDirector(Duel duel, CombatDuelActorSubCircle[] actorSubCircles) {
        _duel = duel;
        _subCircles = actorSubCircles;
        _effects = new CombatEffectApplicator(actorSubCircles);
    }

    public void Reset() {
        // Reset the rounds combat action list.
        _queuedCombatActions = new List<QueuedCombatAction>();
    }

    public uint GetQueuedCombatActionsTime() {
        var count = 0.0f;

        // Every spell takes 10 seconds.
        foreach (var action in _queuedCombatActions) {
            if (action.Spell != null) {
                if (!action.PredeterminedSuccess) {
                    count += SPELL_FIZZLE_TIME;
                    continue;
                }

                count += SPELL_CAST_TIME;
                count += GetActionCinematicTime(action);
            }
        }

        // Add how many subcircles are alive and passing their turn.
        count += (ActiveSubCircles.Where(x => x.IsAlive).Count() - _queuedCombatActions.Count) * SPELL_PASS_TIME;

        return (uint) count;
    }

    public CombatActionListObj ApplyQueuedCombatActions() {
        Logger.Debug("Duel {0} | Combat actions round {1}: ", Logger.Args(_duel.m_duelID, _duel.m_roundNum));

        var combatActionList = new CombatActionListObj { m_actionList = new List<CombatAction>() };

        // Some subcircles may not have queued actions. Ensure they do by adding a pass action.
        EnsureAllCastersHaveQueuedActions();

        SortQueuedActions();
        ProcessQueuedActions(combatActionList);
        LogCombatActions(combatActionList);

        return combatActionList;
    }

    public void AddCombatMove(CombatMoveType type, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target, Spell spell) {
        // If this spell is already queued by the same caster, remove all of their queued actions.
        _queuedCombatActions.RemoveAll(x => x.SpellCaster == caster);

        if (type == CombatMoveType.ChangeMind) {
            return;
        }

        // Determine if the spell fizzles.
        var spellHits = spell is not null && SpellHits(caster, spell);

        var queuedAction = new QueuedCombatAction {
            SpellCaster = caster,
            TargetSubcircle = target,
            Spell = type == CombatMoveType.Attack ? spell : null,
            PredeterminedSuccess = spellHits,
        };
        _queuedCombatActions.Add(queuedAction);

        LogCombatAction(type, caster, target, spell);
    }

    public bool HaveAllPlayersEnqueuedActions(int playerCount) {
        var enqueuedPlayers = _queuedCombatActions.Select(action => action.SpellCaster)
                                                  .Where(subCircle => subCircle.OccupiedTeam == CombatTeam.Player)
                                                  .Distinct();

        return enqueuedPlayers.Count() == playerCount;
    }

    private void EnsureAllCastersHaveQueuedActions() {
        var castersWithoutActions = ActiveSubCircles
            .Where(subCircle => subCircle.AddedToDuel && subCircle.IsAlive)
            .Except(_queuedCombatActions.Select(action => action.SpellCaster))
            .ToList();

        foreach (var subCircle in castersWithoutActions) {
            var queuedAction = new QueuedCombatAction {
                SpellCaster = subCircle,
                TargetSubcircle = subCircle,
                Spell = null
            };
            _queuedCombatActions.Add(queuedAction);
        }
    }

    private void SortQueuedActions() {
        _queuedCombatActions.Sort((a, b) => {
            var aSlot = a.SpellCaster.SlotIndex;
            var bSlot = b.SpellCaster.SlotIndex;

            if ((int) a.SpellCaster.OccupiedTeam == _duel.m_firstTeamToAct) {
                return -1; // Starting team goes first
            }
            else if ((int) b.SpellCaster.OccupiedTeam == _duel.m_firstTeamToAct) {
                return 1;
            }
            else {
                // Within the same team, sort by slot index.
                return aSlot.CompareTo(bSlot);
            }
        });
    }

    private void ProcessQueuedActions(CombatActionListObj combatActionList) {
        foreach (var action in _queuedCombatActions) {
            if (!action.PredeterminedSuccess && action.Spell is not null) {
                HandleFizzleAction(action, combatActionList);
            }
            else {
                HandleSuccessfulAction(action, combatActionList);
            }
        }
    }

    private void HandleFizzleAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var fizzleAction = new CombatAction {
            m_spellCaster = action.SpellCaster.SlotIndex,
            m_targetSubcircleList = new List<int> { action.TargetSubcircle.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 0,
            m_spell = action.Spell,
        };
        combatActionList.m_actionList.Add(fizzleAction);
    }

    private void HandleSuccessfulAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var combatAction = _effects.ApplyCombatAction(action);
        combatActionList.m_actionList.Add(combatAction);

        if (action.Spell is null) {
            return;
        }

        // Remove the caster's pips. If the spell is mastered, power pips count as 2 pips.
        // Remove power pips before generic pips.
        var spell = action.Spell;
        var isMastered = action.SpellCaster.HasSchoolMastery(spell.m_magicSchoolID);
        var pipCount = action.SpellCaster.CombatParticipant.m_pipCount;
        int pipsToDeduct = spell.m_pipCost.m_spellRank;

        while (pipsToDeduct > 0) {
            if (isMastered && pipCount.m_powerPips > 0) {
                pipCount.m_powerPips--;
                pipsToDeduct -= 2;
            }
            else if (!isMastered && pipCount.m_powerPips > 0) {
                pipCount.m_powerPips--;
                pipsToDeduct--;
            }
            else if (pipCount.m_powerPips == 0 && pipCount.m_genericPips > 0) {
                pipCount.m_genericPips--;
                pipsToDeduct--;
            }
            else if (pipCount.m_powerPips == 0 && pipCount.m_genericPips == 0) {
                throw new InvalidOperationException("Not enough pips to cast the spell.");
            }
        }
    }

    private float GetActionCinematicTime(QueuedCombatAction action) {
        if (action.Spell is null) {
            return SPELL_PASS_TIME;
        }

        var spellName = SpellFactory.GetBaseSpellName(action.Spell.m_templateID);
        var cinematicFactory = SpellCinematics.Instance;

        // All spells will always have a summon time.
        var count = cinematicFactory.GetSpellSummonTime(spellName);

        // Check to see if the spell has an act time. If it does, add it to the total time.
        // Otherwise, return the total time.
        var actTime = cinematicFactory.GetSpellActTime(spellName);
        if (actTime <= 0.1f) {
            return cinematicFactory.GetSpellTotalTime(spellName);
        }

        count += actTime;

        // There is a certain amount of hanging effects (traps/shields/blades/prisms) on both the caster and the target.
        // Each of these hanging effects takes 1 second to resolve.
        var casterHangingEffects = action.SpellCaster.HangingEffects;
        var targetHangingEffects = action.TargetSubcircle.HangingEffects;
        var totalHangingEffects = casterHangingEffects.Count + targetHangingEffects.Count;
        count += totalHangingEffects;

        return count;
    }

    private void LogCombatAction(CombatMoveType type, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target, Spell spell) {
        if (type == CombatMoveType.ChangeMind) {
            Logger.Debug("Duel {0} | Slot {1} | Caster changed their mind and is not casting a spell",
                Logger.Args(_duel.m_duelID, caster.SlotIndex));
            return;
        }

        var targetOrSelf = target.SlotIndex == caster.SlotIndex ? "self" : target.SlotIndex.ToString();
        Logger.Debug("Duel {0} | Slot {1} | Queued spell {2} towards target {3}",
            Logger.Args(_duel.m_duelID, caster.SlotIndex, spell.m_templateID, targetOrSelf));
    }

    private void LogCombatActions(CombatActionListObj combatActionList) {
        foreach (var action in combatActionList.m_actionList) {
            // Spell fizzled. Do not log.
            if (action.m_spellHits == (char) 0) {
                Logger.Debug("Duel {0} | Slot {1} | Spell fizzles", Logger.Args(_duel.m_duelID, action.m_spellCaster));
                continue;
            }

            var duelId = _duel.m_duelID;
            var slot = action.m_spellCaster;
            var spell = action.m_spell != null ? action.m_spell.m_templateID.ToString() : "None";
            var target = string.Join(",", action.m_targetSubcircleList ?? new List<int>());

            if (action.m_spell == null) {
                Logger.Debug("Duel {0} | Slot {1} | Passes the turn", Logger.Args(duelId, slot));
            }
            else {
                Logger.Debug("Duel {0} | Slot {1} | Casts spell {2} towards target(s) {3}", Logger.Args(duelId, slot, spell, target));
            }
        }
    }

    private bool SpellHits(CombatDuelActorSubCircle caster, Spell spell) {
        var spellAccuracy = (int) spell.m_accuracy;
        var stats = caster.CombatParticipant.m_pGameStats;
        var school = (MagicSchool) spell.m_magicSchoolID;

        var percentIncrease = caster.GetStatBySchool(stats.m_accBonusPercent, school);
        var percentIncreaseAll = stats.m_accBonusPercentAll;
        var percentDecrease = caster.GetStatBySchool(stats.m_accReducePercent, school);
        var percentDecreaseAll = stats.m_accReducePercentAll;

        // Convert to percentages for calculation
        var totalIncrease = (percentIncrease + percentIncreaseAll) * 100;
        var totalDecrease = (percentDecrease + percentDecreaseAll) * 100;

        // Apply percentages to the spell accuracy
        var newSpellAccuracy = spellAccuracy * (1 + totalIncrease / 100.0) * (1 - totalDecrease / 100.0);

        var hitChance = new Random().Next(0, 100);
        return hitChance <= newSpellAccuracy;
    }
}
