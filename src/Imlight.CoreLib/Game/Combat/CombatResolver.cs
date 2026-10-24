/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Spells;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
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
public class CombatResolver(Duel duel, CombatDuelSubCircle[] actorSubCircles) {
    
    private const float EXECUTION_PHASE_FIXED_OVERHEAD_SECONDS = 1.0f;

    private readonly Duel _duel = duel;

    private readonly CombatDuelSubCircle[] _subCircles = actorSubCircles;
    private CombatDuelSubCircle[] ActiveSubCircles => [.. _subCircles.Where(x => x.Occupied)];
    private List<QueuedCombatAction> _queuedCombatActions;

    /// <summary>
    /// The team that plans and acts now. When set, the other team queues no moves and is not made to pass. When
    /// <c>null</c>, every team acts in the same round.
    /// </summary>
    internal CombatTeam? ActingTeam { get; set; }

    public void Reset() =>
        _queuedCombatActions = [];

    /// <summary>
    /// Applies queued combat actions to the duel, processing each action in order.
    /// </summary>
    /// <param name="combatActionListObj">The object that will hold the list of combat actions.</param>
    /// <returns>The total cinematic time for the actions applied.</returns>
    public float ApplyQueuedCombatActions(out CombatActionListObj combatActionListObj) {
        Logger.Debug("Duel {0} | Applying combat actions..", 
            Logger.Args(_duel.m_duelID.Full, _duel.m_roundNum));

        combatActionListObj = new CombatActionListObj { m_actionList = [] };

        // Some subcircles may not have queued actions. Ensure they do by adding a pass action.
        AddCasterPassActionIfNeeded();
        SortQueuedActions();

        var cinematicTime = ProcessQueuedActions(combatActionListObj);

        return cinematicTime;
    }

    /// <summary>
    /// Adds a combat move to the queue for processing.
    /// </summary>
    /// <param name="type">The type of combat move (e.g., Attack, ChangeMind).</param>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="target">The target of the spell.</param>
    /// <param name="spell">The spell being cast.</param>
    public void AddCombatMove(CombatMoveType type,
                              CombatDuelSubCircle caster,
                              CombatDuelSubCircle target,
                              Spell spell) {
        if (!IsActing(caster)) {
            Logger.Debug("Duel {0} | Slot {1} | Move dropped, the other team is acting.",
                Logger.Args(_duel.m_duelID.Full, caster.SlotIndex));

            return;
        }

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
                    Logger.Args(_duel.m_duelID.Full, caster.SlotIndex, spell.m_templateID));
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

    /// <summary>
    /// Checks if all participants have enqueued actions.
    /// </summary>
    /// <returns>True if all participants have enqueued actions, false otherwise.</returns>
    public bool HaveAllParticipantsEnqueuedActions() {
        var enqueuedPlayers = _subCircles.Where(circle => circle.AddedToDuel && circle.IsAlive && IsActing(circle));

        return enqueuedPlayers.Count() == _queuedCombatActions.Count;
    }

    /// <summary>The move a given caster has queued this round, or null if none. Used to re-broadcast a
    /// summoned minion's AI-chosen move at planning-phase start so the client telegraphs it.</summary>
    public QueuedCombatAction GetQueuedAction(CombatDuelSubCircle caster)
        => _queuedCombatActions.FirstOrDefault(a => a.SpellCaster == caster);

    private bool IsActing(CombatDuelSubCircle circle)
        => ActingTeam is null || circle.OccupiedTeam == ActingTeam;

    private void AddCasterPassActionIfNeeded() {
        var castersWithoutActions = ActiveSubCircles
            .Where(subCircle => subCircle.AddedToDuel && subCircle.IsAlive && IsActing(subCircle))
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
        var instantCinematics = _subCircles[0]._duelActor.CheatInstantCinematics;

        foreach (var action in _queuedCombatActions) {
            // If the caster is dead, skip this action.
            if (!action.SpellCaster.IsAlive) {
                Logger.Debug("Duel {0} | Slot {1} | Caster is dead. Skipping action.",
                    Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex));

                continue;
            }

            // Every participant this action kills plays a death before the next action.
            var aliveBeforeAction = ActiveSubCircles.Where(x => x.IsAlive).ToArray();
            cinematicTime += ProcessQueuedAction(action, combatActionList, cinematicTime);
            cinematicTime += aliveBeforeAction.Count(x => !x.IsAlive) * SpellCinematics.GetDeathTime();
        }

        return instantCinematics ? 0 : cinematicTime + EXECUTION_PHASE_FIXED_OVERHEAD_SECONDS;
    }

    private float ProcessQueuedAction(QueuedCombatAction action, CombatActionListObj combatActionList, float actionStartTime) {
        // We want to invoke the overtime effects after we check for death.
        // This is because the overtime effects can kill a participant, and we want to see the animation.
        var cinematicTime = InvokeOverTimeEffects(action.SpellCaster);

        // A caster an over-time tick kills loses its action; the client moves straight on to the next one.
        if (!action.SpellCaster.IsAlive) {
            Logger.Debug("Duel {0} | Slot {1} | Caster died to an over-time effect. Skipping action.",
                Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex));

            return cinematicTime;
        }

        // A null spell indicates the caster is passing their turn.
        if (action.Spell is null || action.SelectedTarget is null) {
            Logger.Debug("Duel {0} | Slot {1} | Caster is passing their turn.",
                Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex));

            return cinematicTime + HandlePassAction(action, combatActionList);
        }

        // If our target is gone or we're stunned, pass the turn.
        // todo: the client ticks m_stunned on a turn only in alternating-turn duels, and plays its 2 s Unstunned stage on the first
        // turn after a stun when m_stunnedDisplay is set; the server neither sets m_stunnedDisplay nor times that stage.
        if (    action.SpellCaster.CombatParticipant.m_stunned > 0 
            || !action.SelectedTarget.IsAlive
            || !action.SelectedTarget.AddedToDuel) {
            action.SpellCaster.CombatParticipant.m_stunned--;

            Logger.Debug("Duel {0} | Slot {1} | Spell cannot occur because target is dead or caster is stunned.",
                Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex));

            return cinematicTime + HandlePassAction(action, combatActionList);
        }

        // Determine if this spell hits or fizzles.
        if (!SpellHits(action.SpellCaster, action.Spell)) {
            // Increase pips used counter by 1, even if the spell fizzled.
            action.SpellCaster._usedPipsForExperienceGain++;

            return cinematicTime + HandleFizzleAction(action, combatActionList);
        }

        // Record when this caster's cinematic begins so a summoned minion appears with its cast.
        action.SpellCaster._duelActor.CurrentActionCinematicOffsetSeconds = actionStartTime + cinematicTime;

        return cinematicTime + HandleSuccessfulAction(action, combatActionList);
    }

    private float HandleFizzleAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var fizzleAction = InitializeCombatAction(action);
        fizzleAction.m_spellHits = (char) 0;
        fizzleAction.m_targetSubcircleList.Add(action.SelectedTarget.SlotIndex);
        combatActionList.m_actionList.Add(fizzleAction);

        Logger.Debug("Duel {0} | Slot {1} | Spell fizzled.",
            Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex));

        return SpellCinematics.GetFizzleTime(SpellFactory.GetBaseSpellName(action.Spell.m_templateID));
    }

    private float HandleSuccessfulAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var cinematicTime = 0.0f;
        var combatAction = InitializeCombatAction(action);
        var spellWorthCasting = CombatActionResolver.ProcessedQueuedCombatAction(action, ref combatAction, ref cinematicTime);

        LogCombatAction(action, combatAction, spellWorthCasting);

        combatActionList.m_actionList.Add(combatAction);

        if (action.Spell is null) {
            return SpellCinematics.GetPassTime();
        }

        DoSpellCastConsequences(action.SpellCaster, combatAction);

        // A spell not worth casting goes out without its spell, which the client plays as a pass.
        if (combatAction.m_spell is null) {
            return SpellCinematics.GetPassTime();
        }

        return GetActionCinematicTime(action, combatAction) + cinematicTime;
    }

    private float HandlePassAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var passCombatAction = InitializeCombatAction(action);
        passCombatAction.m_spell = null;
        passCombatAction.m_spellHits = (char) 0;
        combatActionList.m_actionList.Add(passCombatAction);

        return SpellCinematics.GetPassTime();
    }

    private float InvokeOverTimeEffects(CombatDuelSubCircle caster) {
        // Get all DoT and HoT effects. Clone the list to avoid concurrent modification.
        var dotEffects = caster._hangingEffects
            .Where(x => x.m_effectType == kSpellEffects.kDamageOverTime)
            .ToList();
        var hotEffects = caster._hangingEffects
            .Where(x => x.m_effectType == kSpellEffects.kHealOverTime)
            .ToList();
        var cinematicTime = (dotEffects.Count + hotEffects.Count) * SpellCinematics.GetOverTimeTickTime();

        foreach (var effect in dotEffects.Concat(hotEffects)) {
            var amount = CombatHangingEffects.AdvanceOverTime(effect);
            cinematicTime += CombatHangingEffects.ApplyOverTimeTick(caster, effect, amount);

            // Remove the effect once its ticks are used up.
            if (effect.m_effectParam <= 0) {
                caster._hangingEffects.Remove(effect);
            }
        }

        return cinematicTime;
    }

    private void LogQueuedCombatAction(CombatMoveType type, CombatDuelSubCircle caster, CombatDuelSubCircle target, Spell spell) {
        if (type == CombatMoveType.ChangeMind) {
            Logger.Debug("Duel {0} | Slot {1} | Caster changed their mind and is not casting a spell",
                Logger.Args(_duel.m_duelID.Full, caster.SlotIndex));

            return;
        }

        var targetOrSelf = target is null
            ? "null" : (target.SlotIndex == caster.SlotIndex ? "self" : target.SlotIndex.ToString());
        var spellOrPass = spell is null ? "pass" : spell.m_templateID.ToString();
        Logger.Debug("Duel {0} | Slot {1} | Caster is casting spell {2} against target {3}",
            Logger.Args(_duel.m_duelID.Full, caster.SlotIndex, spellOrPass, targetOrSelf));
    }

    private void LogCombatAction(QueuedCombatAction action, CombatAction combatAction, bool spellWorthCasting) {
        if (spellWorthCasting) {
            var targetsStringForLog = string.Join(", ", combatAction.m_targetSubcircleList);
            var critSuffix = combatAction.m_CritHitList?.Any(x => x.m_mult > 0f) == true ? " (Crits!)" : "";
            Logger.Debug("Duel {0} | Slot {1} | Spell {2} hits targets [{3}]{4}",
                Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex, action.Spell.m_templateID, targetsStringForLog, critSuffix));
        }
        else {
            Logger.Debug("Duel {0} | Slot {1} | Spell {3} not worth casting. Passing turn.",
                Logger.Args(_duel.m_duelID.Full, action.SpellCaster.SlotIndex, action.Spell.m_templateID));
            combatAction.m_spell = null;
        }
    }

    private static CombatAction InitializeCombatAction(QueuedCombatAction action) => new() {
        m_spellCaster = action.SpellCaster.SlotIndex,
        m_targetSubcircleList = [],
        m_showCast = true,
        m_spellHits = (char) 1,
        m_spell = action.Spell,
        m_serializedBlocks = "\u0000\u0000",
        m_shadowPactTarget = -1,
        m_petCastTarget = -1,
        m_CritHitList = [],
    };

    private static float GetActionCinematicTime(QueuedCombatAction action, CombatAction combatAction) {
        var spellName = SpellFactory.GetBaseSpellName(action.Spell.m_templateID);
        var castTime = SpellCinematics.GetCastTime(spellName) + SpellCinematics.GetSpellPlayTime(spellName);

        // The client queues one critical hit event per action, not one per critted target.
        var landedCritical = combatAction.m_CritHitList?.Any(x => x.m_mult > 0f) ?? false;
        if (!landedCritical) {
            return castTime;
        }

        return castTime + SpellCinematics.GetCriticalHitTime(spellName);
    }

    private static bool SpellHits(CombatDuelSubCircle caster, Spell spell) {
        if (caster is null || spell is null) {
            return false;
        }

        if (ConsumeDispell(caster, spell.m_magicSchoolID)) {
            return false;
        }

        if (caster.CheatNoFizzle) {
            return true;
        }

        var spellAccuracy = (int) spell.m_accuracy;
        var stats = caster.ResolverGameStats;
        var school = MagicSchools.GetMagicSchool(spell.m_magicSchoolID);
        var schoolName = school.m_schoolName;

        var percentIncrease = caster.GetStatBySchool(stats.m_accBonusPercent, schoolName);
        var percentIncreaseAll = stats.m_accBonusPercentAll;
        var percentDecrease = caster.GetStatBySchool(stats.m_accReducePercent, schoolName);
        var percentDecreaseAll = stats.m_accReducePercentAll;

        // Convert to percentages for calculation
        var totalIncrease = (percentIncrease + percentIncreaseAll) * 100;
        var totalDecrease = (percentDecrease + percentDecreaseAll) * 100;

        // Apply percentages to the spell accuracy
        spellAccuracy *= (int) Math.Floor((1 + totalIncrease / 100.0) * (1 - totalDecrease / 100.0));

        // Apply any hanging accuracy effects
        spellAccuracy = ConsumeHangingAccuracyEffects(spellAccuracy, caster, spell.m_magicSchoolID);

        var hitChance = Random.Shared.Next(0, 100);
        
        return hitChance <= spellAccuracy;
    }

    private static void DoSpellCastConsequences(CombatDuelSubCircle caster, CombatAction action) {
        if (action.m_spell is null) {
            return;
        }

        // Treasure cards are permanently consumed on successful cast.
        // Regular spells are discarded and return to the deck on reshuffle.
        if (action.m_spell.m_treasureCard) {
            var consumedTemplateId = caster.ConsumeFromVault(action.m_spell);
            if (consumedTemplateId != 0 && caster._wizard != null && !caster.IsSandboxed) {
                // Persist the removal from the player's treasure card book.
                caster._wizard.SpellbookBehavior.RemoveTreasureCard(consumedTemplateId);
                WizardCollection.RemoveTreasureCard(caster._wizard, consumedTemplateId);

                // Also remove from the equipped deck's spell list so it doesn't
                // reappear when the player re-opens their deck after combat.
                var deckSlot = caster._wizard.EquipmentBehavior.SlotList
                    .FirstOrDefault(s => s.SlotType == EquipmentSlotType.Deck);
                if (deckSlot?.ItemId != null) {
                    caster._wizard.RemoveSpellFromDeck(consumedTemplateId, deckSlot.ItemId.Value);

                    // Tell the client to remove the TC from the deck UI.
                    var spellTemplate = CoreObjectFactory.GetCoreTemplate(consumedTemplateId) as SpellTemplate;
                    if (spellTemplate != null) {
                        var spellHash = StringHash.Compute(spellTemplate.m_name);
                        caster.ParticipantActor.Tell(
                            new WIZARD_12_PROTOCOL.MSG_REMOVETREASURESPELLFROMDECK {
                                SpellID = (int) spellHash,
                                EnchantmentID = 0,
                                DeckID = deckSlot.ItemId.Value,
                                Success = 1,
                                Destroy = 0
                            }, ActorRefs.NoSender);
                    }
                }
            }
        }
        else {
            caster.DiscardCard(action.m_spell);
        }

        // Deduce the players mana by the rank of the spell.
        caster.DeductMana(action.m_spell.m_pipCost.m_spellRank);

        // X-pip spells spend what they used (same GetXPipCost that chose the tier).
        if (CombatActionResolver.IsXPipSpell(action.m_spell)) {
            var xCost = CombatActionResolver.GetXPipCost(action.m_spell, caster);
            caster._usedPipsForExperienceGain += xCost;

            caster.DeductPips((MagicSchool) action.m_spell.m_magicSchoolID, xCost);
        }
        else {
            // Increase the used pips for experience gain by the rank of the spell.
            // Even 0-rank spells will give the caster 1 pip for experience gain.
            caster._usedPipsForExperienceGain += Math.Max((byte) 1, action.m_spell.m_pipCost.m_spellRank);

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
            .Where(x => x.m_damageType == magicSchoolId)
            .ToList();

        foreach (var effect in accuracyHangingEffects) {
            startingAccuracy += (int) Math.Floor(1 + effect.m_effectParam / 100.0);
            caster._hangingEffects.Remove(effect);
        }

        return startingAccuracy;
    }

}
