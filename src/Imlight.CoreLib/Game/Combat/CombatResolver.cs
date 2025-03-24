/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * COMBAT ACTION RESOLUTION SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Serves as the entry point for resolving all combat actions, coordinating
 * the sequence of spells cast during a round and implementing the core
 * mathematical resolution of combat effects.
 * 
 * USAGE EXAMPLE:
 * var resolver = new CombatResolver(duel, subCircles);
 * resolver.Reset();
 * resolver.AddCombatMove(CombatMoveType.Attack, caster, target, spell);
 * float cinematicTime = resolver.ApplyQueuedCombatActions(out combatActionListObj);
 * 
 * NOTE:
 * This system works in conjunction with several specialized combat classes:
 * 
 * - CombatDuelComponent:    Orchestrates the overall duel, manages participants and phases
 * - CombatResolver:         Processes and resolves combat actions during the execution phase
 * - CombatDuelSubCircle:    Handles individual participant state and position
 * - CombatActionResolver:   Processes queued actions and resolves target selection
 * - CombatEffectApplicator: Applies spell effects with proper modifications
 * - CombatCharms:           Manages offensive modifiers that affect outgoing damage/healing
 * - CombatWards:            Handles defensive modifiers that affect incoming damage
 * - CombatDeck:             Controls spell deck management, drawing and discarding
 * - CombatEffectStack:      Tracks random/variable effect selection using bit-packing
 * 
 * The resolver determines hit/fizzle mechanics, processes spell accuracy,
 * manages the execution order, and coordinates timing of animations.
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.Spells;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Cryptography;

namespace Imlight.CoreLib.Game.Combat;

public class QueuedCombatAction {
    
    public CombatDuelSubCircle SpellCaster;
    public CombatDuelSubCircle SelectedTarget;
    public Spell Spell;
    public SpellTemplate SpellTemplate;
    
}

/// <summary>
/// Processes and resolves combat actions during the execution phase of a duel round.
/// </summary>
/// <remarks>
/// Responsible for determining the order of combat actions, handling fizzles, and coordinating 
/// the effects of spells on targets. Manages the flow of a combat turn by processing queued 
/// actions and calculating cinematic timing for visual feedback.
/// </remarks>
public class CombatResolver {
    
    private const int SPELL_FIZZLE_TIME = 4;
    private const int SPELL_PASS_TIME = 1;
    private const float SPELL_CAST_TIME = 5.0f;
    private const float HANGING_EFFECT_CONSUME_TIME = 1.0f;
    private const float OVER_TIME_ACTIVATION_TIME = 2.0f;
    private const float DEATH_ANIMATION_TIME = 2.0f;

    private readonly Duel _duel;

    private readonly CombatDuelSubCircle[] _subCircles = new CombatDuelSubCircle[8];
    private CombatDuelSubCircle[] ActiveSubCircles => [.. _subCircles.Where(x => x.Occupied)];
    private List<QueuedCombatAction> _queuedCombatActions;

    // ctor
    public CombatResolver(Duel duel, CombatDuelSubCircle[] actorSubCircles) {
        _duel = duel;
        _subCircles = actorSubCircles;
    }

    public void Reset() =>
        // Reset the rounds combat action list.
        _queuedCombatActions = [];

    public float ApplyQueuedCombatActions(out CombatActionListObj combatActionListObj) {
        Logger.Debug("Duel {0} | Applying combat actions..", Logger.Args(_duel.m_duelID, _duel.m_roundNum));

        combatActionListObj = new CombatActionListObj { m_actionList = new List<CombatAction>() };

        // Some subcircles may not have queued actions. Ensure they do by adding a pass action.
        AddCasterPassActionIfNeeded();
        SortQueuedActions();

        var cinematicTime = ProcessQueuedActions(combatActionListObj);

        return cinematicTime;
    }

    public void AddCombatMove(CombatMoveType type,
                              CombatDuelSubCircle caster,
                              CombatDuelSubCircle target,
                              Spell spell) {
        // If this spell is already queued by the same caster, remove all of their queued actions.
        _queuedCombatActions.RemoveAll(x => x.SpellCaster == caster);

        if (type == CombatMoveType.ChangeMind) {
            // We can immediately return here. Anytime a caster doesn't have a queued action, they will pass their turn.
            return;
        }

        // Get the spell template.
        SpellTemplate spellTemplate = null;
        if (spell is not null) {
            spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);

            if (spellTemplate is null) {
                Logger.Error("Duel {0} | Slot {1} | Spell template {2} not found",
                    Logger.Args(_duel.m_duelID, caster.SlotIndex, spell.m_templateID));
                return;
            }
        }

        var queuedAction = new QueuedCombatAction {
            SpellCaster = caster,
            Spell = type == CombatMoveType.Attack ? spell : null,
            SpellTemplate = spellTemplate,
            SelectedTarget = target
        };
        _queuedCombatActions.Add(queuedAction);

        LogQueuedCombatAction(type, caster, target, spell);
    }

    public bool HaveAllParticipantsEnqueuedActions() {
        var enqueuedPlayers = _subCircles.Where(circle => circle.AddedToDuel && circle.IsAlive);

        return enqueuedPlayers.Count() == _queuedCombatActions.Count;
    }

    private void AddCasterPassActionIfNeeded() {
        var castersWithoutActions = ActiveSubCircles
            .Where(subCircle => subCircle.AddedToDuel && subCircle.IsAlive)
            .Except(_queuedCombatActions.Select(action => action.SpellCaster))
            .ToList();

        foreach (var subCircle in castersWithoutActions) {
            var queuedAction = new QueuedCombatAction {
                SpellCaster = subCircle,
                SelectedTarget = null,
                Spell = null
            };
            _queuedCombatActions.Add(queuedAction);
        }
    }

    private void SortQueuedActions() => _queuedCombatActions.Sort((a, b) => {
        var aSlot = a.SpellCaster.SlotIndex;
        var bSlot = b.SpellCaster.SlotIndex;

        var aTeam = (int) a.SpellCaster.OccupiedTeam;
        var bTeam = (int) b.SpellCaster.OccupiedTeam;

        // Check if both actions belong to the same team
        if (aTeam == bTeam) {
            // Within the same team, sort by slot index (ascending)
            return aSlot.CompareTo(bSlot);
        }
        else {
            // Teams are different, prioritize team who acts first
            if (aTeam == _duel.m_firstTeamToAct) {
                return -1; // Team a acts first
            }
            else if (bTeam == _duel.m_firstTeamToAct) {
                return 1; // Team b acts first
            }
            else {
                return 0; // This should not happen.
            }
        }
    });

    private float ProcessQueuedActions(CombatActionListObj combatActionList) {
        var cinematicTime = 0.0f;

        foreach (var action in _queuedCombatActions) {
            // If the caster is dead, skip this action.
            if (!action.SpellCaster.IsAlive) {
                Logger.Debug("Duel {0} | Slot {1} | Caster is dead. Skipping action.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));
                continue;
            }

            // We want to invoke the overtime effects after we check for death.
            // This is because the overtime effects can kill a participant, and we want to see the animation.
            cinematicTime += InvokeOverTimeEffects(action.SpellCaster);

            // A null spell indicates the caster is passing their turn.
            if (action.Spell is null || action.SelectedTarget is null) {
                Logger.Debug("Duel {0} | Slot {1} | Caster is passing their turn.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

                cinematicTime += HandlePassAction(action, combatActionList);

                continue;
            }

            // If our target is gone or we're stunned, pass the turn.
            if (    action.SpellCaster.CombatParticipant.m_stunned > 0 
                || !action.SelectedTarget.IsAlive
                || !action.SelectedTarget.AddedToDuel) {
                action.SpellCaster.CombatParticipant.m_stunned--;

                cinematicTime += HandlePassAction(action, combatActionList);

                Logger.Debug("Duel {0} | Slot {1} | Spell cannot occur because target is dead or caster is stunned.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

                continue;
            }

            // Determine if this spell hits or fizzles.
            var spellHits = SpellHits(action.SpellCaster, action.Spell);
            if (!spellHits) {
                cinematicTime += HandleFizzleAction(action, combatActionList);
            }
            else {
                cinematicTime += HandleSuccessfulAction(action, combatActionList);
            }
        }

        return cinematicTime;
    }

    private float HandleFizzleAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var fizzleAction = InitializeCombatAction(action);
        fizzleAction.m_spellHits = (char) 0;
        fizzleAction.m_targetSubcircleList.Add(action.SelectedTarget.SlotIndex);
        combatActionList.m_actionList.Add(fizzleAction);

        Logger.Debug("Duel {0} | Slot {1} | Spell fizzled.",
            Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

        return SPELL_FIZZLE_TIME;
    }

    private float HandleSuccessfulAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var cinematicTime = 0.0f;
        var combatAction = InitializeCombatAction(action);
        var spellWorthCasting = CombatActionResolver.ProcessedQueuedCombatAction(action, ref combatAction, ref cinematicTime);

        LogCombatAction(action, combatAction, spellWorthCasting);

        combatActionList.m_actionList.Add(combatAction);

        if (action.Spell is null) {
            return SPELL_PASS_TIME;
        }

        DoSpellCastConsequences(action.SpellCaster, combatAction);

        return GetActionCinematicTime(action) + cinematicTime;
    }

    private float HandlePassAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var passCombatAction = InitializeCombatAction(action);
        passCombatAction.m_spell = null;
        combatActionList.m_actionList.Add(passCombatAction);

        return SPELL_PASS_TIME;
    }

    private float InvokeOverTimeEffects(CombatDuelSubCircle caster) {
        // Get all DoT and HoT effects. Clone the list to avoid concurrent modification.
        var dotEffects = caster._hangingEffects.Where(x => x.m_effectType == kSpellEffects.kDamageOverTime).ToList();
        var hotEffects = caster._hangingEffects.Where(x => x.m_effectType == kSpellEffects.kHealOverTime).ToList();
        var cinematicTime = (dotEffects.Count + hotEffects.Count) * OVER_TIME_ACTIVATION_TIME;

        foreach (var effect in dotEffects) {
            var initialDamage = effect.m_paramPerRound;
            var wards = CombatWards.FindAppliedWards(caster, effect).ToList();
            var damage = CombatWards.GetIncomingDamageFromWards(wards, initialDamage);

            // We don't need to calculate stats from gear because the initial application already did that.

            cinematicTime += HANGING_EFFECT_CONSUME_TIME * wards.Count;
            caster.DamageParticipant(damage);
            effect.m_numRounds--;

            // Remove the effect if it's out of rounds.
            if (effect.m_numRounds <= 0) {
                caster._hangingEffects.Remove(effect);
            }
        }

        foreach (var effect in hotEffects) {
            // Todo: are there wards that increase incoming healing?
            // We don't need to calculate stats from gear because the initial application already did that.
            caster.HealParticipant(effect.m_paramPerRound);
            effect.m_numRounds--;

            // Remove the effect if it's out of rounds.
            if (effect.m_numRounds <= 0) {
                caster._hangingEffects.Remove(effect);
            }
        }

        // Is the participant dead after the effects?
        // If so, add a death animation time.
        if (!caster.IsAlive) {
            cinematicTime += DEATH_ANIMATION_TIME;
        }

        return cinematicTime;
    }

    private void LogQueuedCombatAction(CombatMoveType type, CombatDuelSubCircle caster, CombatDuelSubCircle target, Spell spell) {
        if (type == CombatMoveType.ChangeMind) {
            Logger.Debug("Duel {0} | Slot {1} | Caster changed their mind and is not casting a spell",
                Logger.Args(_duel.m_duelID, caster.SlotIndex));

            return;
        }

        var targetOrSelf = target is null
            ? "null" : (target.SlotIndex == caster.SlotIndex ? "self" : target.SlotIndex.ToString());
        var spellOrPass = spell is null ? "pass" : spell.m_templateID.ToString();
        Logger.Debug("Duel {0} | Slot {1} | Caster is casting spell {2} against target {3}",
            Logger.Args(_duel.m_duelID, caster.SlotIndex, spellOrPass, targetOrSelf));
    }

    private void LogCombatAction(QueuedCombatAction action, CombatAction combatAction, bool spellWorthCasting) {
        if (spellWorthCasting) {
            var targetsStringForLog = string.Join(", ", combatAction.m_targetSubcircleList);
            Logger.Debug("Duel {0} | Slot {1} | Spell {2} hits targets [{3}]",
                Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex, action.Spell.m_templateID, targetsStringForLog));
        }
        else {
            Logger.Debug("Duel {0} | Slot {1} | Spell {3} not worth casting. Passing turn.",
                Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex, action.Spell.m_templateID));
            combatAction.m_spell = null;
        }
    }

    private static CombatAction InitializeCombatAction(QueuedCombatAction action) => new() {
        m_spellCaster = action.SpellCaster.SlotIndex,
        m_targetSubcircleList = new List<int>(),
        m_showCast = true,
        m_spellHits = (char) 1,
        m_spell = action.Spell,
    };

    private static float GetActionCinematicTime(QueuedCombatAction action) {
        if (action.Spell is null) {
            return SPELL_PASS_TIME;
        }

        var spellName = SpellFactory.GetBaseSpellName(action.Spell.m_templateID);
        var cinematicFactory = SpellCinematics.Instance;

        // All spells will always have a summon time.
        var count = cinematicFactory.GetSpellSummonTime(spellName);

        // Check if this spell has a special casting time. If not, just add the default casting time.
        var castTime = cinematicFactory.GetSpellCastingTime(spellName);
        count += castTime > 0.1f ? castTime : SPELL_CAST_TIME;

        // Check to see if the spell has an act time. If it does, add it to the total time.
        // Otherwise, return the total time.
        var actTime = cinematicFactory.GetSpellActTime(spellName);
        if (actTime <= 0.1f) {
            return count + cinematicFactory.GetSpellTotalTime(spellName);
        }

        count += actTime;

        return count;
    }

    private static bool SpellHits(CombatDuelSubCircle caster, Spell spell) {
        if (caster is null || spell is null) {
            return false;
        }

        // Easter egg: Kevin has a 100% fizzle rate on storm spells. Fuck you, Kevin.
        if (caster.OccupiedTeam == CombatTeam.Player && caster.Occupied) {
            var wizardName = caster._wizard.PlayerNameBehavior.GetWizardName();
            var wizardSchool = caster._wizard.MagicSchoolBehavior.MagicSchool;
            var isStormKevin = wizardName == "Kevin" && wizardSchool == MagicSchool.Storm;

            if (isStormKevin && spell.m_magicSchoolID == (uint) MagicSchool.Storm) {
                return false;
            }
        }

        if (ConsumeDispell(caster, spell.m_magicSchoolID)) {
            return false;
        }

        var spellAccuracy = (int) spell.m_accuracy;
        var stats = caster.CombatParticipant.m_pGameStats;
        var school = MagicSchools.GetMagicSchool(spell.m_magicSchoolID).m_schoolName;

        var percentIncrease = caster.GetStatBySchool(stats.m_accBonusPercent, school);
        var percentIncreaseAll = stats.m_accBonusPercentAll;
        var percentDecrease = caster.GetStatBySchool(stats.m_accReducePercent, school);
        var percentDecreaseAll = stats.m_accReducePercentAll;

        // Convert to percentages for calculation
        var totalIncrease = (percentIncrease + percentIncreaseAll) * 100;
        var totalDecrease = (percentDecrease + percentDecreaseAll) * 100;

        // Apply percentages to the spell accuracy
        spellAccuracy *= (int) Math.Floor((1 + totalIncrease / 100.0) * (1 - totalDecrease / 100.0));

        // Apply any hanging accuracy effects
        spellAccuracy = ConsumeHangingAccuracyEffects(spellAccuracy, caster, spell.m_magicSchoolID);

        var hitChance = new Random().Next(0, 100);
        return hitChance <= spellAccuracy;
    }

    private static void DoSpellCastConsequences(CombatDuelSubCircle caster, CombatAction action) {
        if (action.m_spell is null) {
            return;
        }

        // If this spell action us successful, remove it from the combat deck of the caster.
        // Deduce the players mana by the rank of the spell.
        caster.DiscardCard(action.m_spell);
        caster.DeductMana(action.m_spell.m_pipCost.m_spellRank);

        // Reduce pips.
        if (action.m_spell.m_pipCost.m_xPipSpell) {
            caster.DeductAllPips();
        }
        else {
            caster.DeductPips((MagicSchool) action.m_spell.m_magicSchoolID, action.m_spell.m_pipCost.m_spellRank);
        }
    }

    private static bool ConsumeDispell(CombatDuelSubCircle caster, uint magicSchoolId) {
        var dispellHangingEffect = caster._hangingEffects
            .FirstOrDefault(x => x.m_effectType == kSpellEffects.kDispel
                     && StringHash.Compute(x.m_sDamageType) == magicSchoolId);

        if (dispellHangingEffect is not null) {
            caster._hangingEffects.Remove(dispellHangingEffect);
            return true;
        }
        else {
            return false;
        }
    }

    private static int ConsumeHangingAccuracyEffects(int startingAccuracy, CombatDuelSubCircle caster, uint magicSchoolId) {
        var accuracyHangingEffects = caster._hangingEffects
            .Where(x => x.m_effectType == kSpellEffects.kModifyAccuracy)
            .Where(x => x.m_damageType == magicSchoolId);

        foreach (var effect in accuracyHangingEffects) {
            startingAccuracy += (int) Math.Floor(1 + effect.m_effectParam / 100.0);
            caster._hangingEffects.Remove(effect);
        }

        return startingAccuracy;
    }

}
