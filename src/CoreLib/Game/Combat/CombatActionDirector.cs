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

namespace Imlight.CoreLib.Game.Combat;

public class QueuedCombatAction {
    public CombatDuelActorSubCircle SpellCaster;
    public CombatDuelActorSubCircle TargetSubcircle;
    public Spell Spell;
    public SpellTemplate SpellTemplate;
}

public class CombatActionDirector {
    private const int SPELL_FIZZLE_TIME = 4;
    private const int SPELL_PASS_TIME = 1;
    private const float SPELL_CAST_TIME = 5.0f;
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

    public float ApplyQueuedCombatActions(out CombatActionListObj combatActionListObj) {
        Logger.Debug("Duel {0} | Applying combat actions..", Logger.Args(_duel.m_duelID, _duel.m_roundNum));

        combatActionListObj = new CombatActionListObj { m_actionList = new List<CombatAction>() };

        // Some subcircles may not have queued actions. Ensure they do by adding a pass action.
        EnsureAllCastersHaveQueuedActions();
        RemoveEmptyTargetActions();
        SortQueuedActions();

        var cinematicTime = ProcessQueuedActions(combatActionListObj);
        return cinematicTime;
    }

    public void AddCombatMove(CombatMoveType type,
                              CombatDuelActorSubCircle caster,
                              CombatDuelActorSubCircle target,
                              Spell spell) {
        // If this spell is already queued by the same caster, remove all of their queued actions.
        _queuedCombatActions.RemoveAll(x => x.SpellCaster == caster);

        if (type == CombatMoveType.ChangeMind) {
            return;
        }

        // Get the spell template.
        SpellTemplate spellTemplate = null;
        if (spell is not null) {
            spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.m_templateID);
        }

        var queuedAction = new QueuedCombatAction {
            SpellCaster = caster,
            TargetSubcircle = target,
            Spell = type == CombatMoveType.Attack ? spell : null,
            SpellTemplate = spellTemplate,
        };
        _queuedCombatActions.Add(queuedAction);

        LogQueuedCombatAction(type, caster, target, spell);
    }

    public bool HaveAllParticipantsEnqueuedActions(int participantCount) {
        var enqueuedPlayers = _subCircles.Where(circle => circle.AddedToDuel && circle.IsAlive);
        return enqueuedPlayers.Count() == _queuedCombatActions.Count;
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

    private void RemoveEmptyTargetActions() {
        // Remove any queued actions that have a target that is not in the duel.
        _queuedCombatActions.RemoveAll(action => action.TargetSubcircle is not null && !action.TargetSubcircle.AddedToDuel);
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
            if (action.TargetSubcircle is null) {
                combatActionList.m_actionList.Add(new CombatAction {
                    m_spellCaster = action.SpellCaster.SlotIndex,
                    m_targetSubcircleList = new List<int> { action.SpellCaster.SlotIndex },
                    m_showCast = true,
                    m_spellHits = (char) 0,
                    m_spell = null,
                });
                continue;
            }

            // If the caster or target is dead, skip this action.
            if (!action.SpellCaster.IsAlive || !action.TargetSubcircle.IsAlive) {
                Logger.Debug("Duel {0} | Slot {1} | Caster or target is dead. Skipping action.",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));
                continue;
            }

            // Determine if this spell hits or fizzles.
            var spellHits = SpellHits(action.SpellCaster, action.Spell);
            if (!spellHits && action.Spell is not null) {
                Logger.Debug("Duel {0} | Slot {1} | Spell fizzles", Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex));

                cinematicTime += HandleFizzleAction(action, combatActionList);
            }
            else {
                Logger.Debug("Duel {0} | Slot {1} | Spell hits against target {2}",
                    Logger.Args(_duel.m_duelID, action.SpellCaster.SlotIndex, action.TargetSubcircle.SlotIndex));

                cinematicTime += HandleSuccessfulAction(action, combatActionList);
            }
        }

        return cinematicTime;
    }

    private float HandleFizzleAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var fizzleAction = new CombatAction {
            m_spellCaster = action.SpellCaster.SlotIndex,
            m_targetSubcircleList = new List<int> { action.TargetSubcircle.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 0,
            m_spell = action.Spell,
        };
        combatActionList.m_actionList.Add(fizzleAction);

        return SPELL_FIZZLE_TIME;
    }

    private float HandleSuccessfulAction(QueuedCombatAction action, CombatActionListObj combatActionList) {
        var effectCinematicTime = _effects.ApplyCombatAction(action, out var combatAction);
        combatActionList.m_actionList.Add(combatAction);

        if (action.Spell is null) {
            return SPELL_PASS_TIME;
        }

        // If this spell action us successful, remove it from the combat deck of the caster.
        action.SpellCaster.DiscardCard(action.Spell);

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
                combatAction.m_spell = null;
            }
        }

        // Return how long the cinematic will take to play out.
        return GetActionCinematicTime(action) + effectCinematicTime;
    }

    private float GetActionCinematicTime(QueuedCombatAction action) {
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

    private void LogQueuedCombatAction(CombatMoveType type, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target, Spell spell) {
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

    private bool SpellHits(CombatDuelActorSubCircle caster, Spell spell) {
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

    private static bool ConsumeDispell(CombatDuelActorSubCircle caster, uint magicSchoolId) {
        var dispellHangingEffect = caster.HangingEffects
            .FirstOrDefault(x => x.m_effectType == SpellEffect.kSpellEffects.kDispel
                     && StringHash.Compute(x.m_sDamageType) == magicSchoolId);

        if (dispellHangingEffect is not null) {
            caster.HangingEffects.Remove(dispellHangingEffect);
            return true;
        }
        else {
            return false;
        }
    }

    private static int ConsumeHangingAccuracyEffects(int startingAccuracy, CombatDuelActorSubCircle caster, uint magicSchoolId) {
        var accuracyHangingEffects = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyAccuracy)
            .Where(x => x.m_damageType == magicSchoolId);

        foreach (var effect in accuracyHangingEffects) {
            startingAccuracy += (int) Math.Floor(1 + effect.m_effectParam / 100.0);
            caster.HangingEffects.Remove(effect);
        }

        return startingAccuracy;
    }
}
