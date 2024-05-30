/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Game.Spells;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Combat;

public class QueuedCombatAction {
    public CombatDuelSubCircle SpellCaster;
    public CombatDuelSubCircle SelectedTarget;
    public Spell Spell;
    public SpellTemplate SpellTemplate;
}

/// <summary>
/// The CombatResolver is responsible for managing the combat actions of the duel.
/// It processes the queued combat actions and applies the effects of the spells to the targets.
/// </summary>
public class CombatResolver {
    private const int SPELL_FIZZLE_TIME = 4;
    private const int SPELL_PASS_TIME = 1;
    private const float SPELL_CAST_TIME = 5.0f;
    private const float HANGING_EFFECT_CONSUME_TIME = 1.0f;
    private const float OVER_TIME_ACTIVATION_TIME = 2.0f;

    private readonly Duel _duel;

    private readonly CombatDuelSubCircle[] _subCircles = new CombatDuelSubCircle[8];
    private CombatDuelSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();
    private List<QueuedCombatAction> _queuedCombatActions;

    // ctor
    public CombatResolver(Duel duel, CombatDuelSubCircle[] actorSubCircles) {
        _duel = duel;
        _subCircles = actorSubCircles;
    }

    public void Reset() {
        // Reset the rounds combat action list.
        _queuedCombatActions = new List<QueuedCombatAction>();
    }

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
            cinematicTime += InvokeOverTimeEffects(action.SpellCaster);

            // If the caster is dead, skip this action.
            if (!action.SpellCaster.IsAlive) {
                Logger.Debug("Duel {0} | Slot {1} | Caster is dead. Skipping action.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));
                continue;
            }

            // If our target is gone or we're stunned, pass the turn.
            if (action.SpellCaster.CombatParticipant.m_stunned > 0) {
                action.SpellCaster.CombatParticipant.m_stunned--;

                var passCombatAction = InitializeCombatAction(action);
                passCombatAction.m_spell = null;
                combatActionList.m_actionList.Add(passCombatAction);

                Logger.Debug("Duel {0} | Slot {1} | Caster is stunned. Passing turn.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

                continue;
            }

            // A null spell indicates the caster is passing their turn.
            if (action.Spell is null) {
                Logger.Debug("Duel {0} | Slot {1} | Caster is passing their turn.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

                cinematicTime += HandlePassAction(action, combatActionList);

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
        fizzleAction.m_targetSubcircleList.Add(action.SpellCaster?.SlotIndex ?? 0);
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
        var dotEffects = caster._hangingEffects.Where(x => x.m_effectType == SpellEffect.kSpellEffects.kDamageOverTime);
        var hotEffects = caster._hangingEffects.Where(x => x.m_effectType == SpellEffect.kSpellEffects.kHealOverTime);
        var cinematicTime = dotEffects.Count() + hotEffects.Count() * OVER_TIME_ACTIVATION_TIME;

        foreach (var effect in dotEffects) {
            var initialDamage = effect.m_paramPerRound;
            var wards = CombatWards.FindAppliedWards(caster, effect);
            var damage = CombatWards.GetIncomingDamageFromWards(wards.ToArray(), initialDamage);

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
        var school = (MagicSchool) spell.m_magicSchoolID;

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
            .FirstOrDefault(x => x.m_effectType == SpellEffect.kSpellEffects.kDispel
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
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyAccuracy)
            .Where(x => x.m_damageType == magicSchoolId);

        foreach (var effect in accuracyHangingEffects) {
            startingAccuracy += (int) Math.Floor(1 + effect.m_effectParam / 100.0);
            caster._hangingEffects.Remove(effect);
        }

        return startingAccuracy;
    }
}
