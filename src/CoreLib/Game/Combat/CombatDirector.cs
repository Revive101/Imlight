/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

public class QueuedCombatAction {
    public DuelActorSubCircle SpellCaster;
    public DuelActorSubCircle TargetSubcircle;
    public Spell Spell;
}

public class CombatDirector {
    private readonly Duel _duel;

    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];
    private DuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();
    private bool _awaitingCombatMoves;
    private List<QueuedCombatAction> _queuedCombatActions;

    // ctor
    public CombatDirector(Duel duel, DuelActorSubCircle[] actorSubCircles) {
        _duel = duel;
        _subCircles = actorSubCircles;
        _duel.m_firstTeamToAct = (int) DetermineFirstTeam();
    }

    public void StartRound() {
        // Reset the rounds combat action list.
        _awaitingCombatMoves = true;
        _queuedCombatActions = new List<QueuedCombatAction>();

        // Determine the power pip gain for each participant.
        EnactActionOnSubCircles(circle => {
            var participant = circle.CombatParticipant;
            var gainedPowerPip = DeterminePowerPipGain(participant);
            if (gainedPowerPip) {
                participant.m_pipCount.m_powerPips++;
            }
            else {
                participant.m_pipCount.m_genericPips++;
            }
        });
    }

    public CombatActionListObj ApplyQueuedCombatActions() {
        var combatActionList = new CombatActionListObj { m_actionList = new List<CombatAction>() };

        // Iterate through each queued combat action and apply the spell effects.
        foreach (var action in _queuedCombatActions) {
            var combatAction = ApplyCombatAction(action);
            combatActionList.m_actionList.Add(combatAction);
        }

        return combatActionList;
    }

    public void EndRound() {
        _awaitingCombatMoves = false;
        _queuedCombatActions = null;
    }

    public CombatPipListObj GetCombatParticipantsPips() {
        var pips = new CombatPipListObj { m_pipList = new List<ParticipantPipData>() };

        EnactActionOnSubCircles(circle => {
            var participantPipData = new ParticipantPipData {
                m_acq = 1,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
                m_pips = new PipCount() {
                    m_genericPips = circle.CombatParticipant.m_pipCount.m_genericPips,
                    m_powerPips = circle.CombatParticipant.m_pipCount.m_powerPips,
                }
            };
            pips.m_pipList.Add(participantPipData);
        });

        return pips;
    }

    public CombatHealthListObj GetCombatParticipantsHealth() {
        // Create the new health list object.
        var healthList = new CombatHealthListObj { m_healthList = new List<ParticipantParameter>() };

        // Iterate through each sub circle and add the participant's health to the list.
        EnactActionOnSubCircles(circle => {
            var participantHealth = new ParticipantParameter {
                m_data = (uint) circle.ParticipantGameStats.m_currentHitpoints,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
            };
            healthList.m_healthList.Add(participantHealth);
        });

        return healthList;
    }

    public void AddCombatMove(DuelActorSubCircle caster, DuelActorSubCircle target, Spell spell) {
        if (!_awaitingCombatMoves) {
            throw new InvalidOperationException("Combat moves are not being accepted at this time.");
        }

        // If this spell is already queued by the same caster, remove their current queued action.
        var existingQueuedAction = _queuedCombatActions.FirstOrDefault(x => x.SpellCaster == caster);
        if (existingQueuedAction != null) {
            _queuedCombatActions.Remove(existingQueuedAction);
            return;
        }

        var queuedAction = new QueuedCombatAction {
            SpellCaster = caster,
            TargetSubcircle = target,
            Spell = spell,
        };
        _queuedCombatActions.Add(queuedAction);
    }

    private Team DetermineFirstTeam() {
        // Flip a coin.
        var random = new Random();
        var result = random.Next(0, 2);
        return (Team) result;
    }

    private bool DeterminePowerPipGain(CombatParticipant participant) {
        var powerPipProbability = participant.m_pGameStats.m_powerPipBase;
        var powerPipChance = new Random().Next(0, 100);
        return powerPipChance <= powerPipProbability;
    }

    private void EnactActionOnSubCircles(Action<DuelActorSubCircle> action) {
        foreach (var subCircle in ActiveSubCircles) {
            action(subCircle);
        }
    }

    private CombatAction ApplyCombatAction(QueuedCombatAction action) {
        var effectStack = new EffectStack();

        foreach (var spellEffect in action.Spell.m_spellEffects) {
            var effect = spellEffect;

            // If this is a random spell effect, we need to determine which effect to use.
            if (spellEffect is RandomSpellEffect randomSpellEffect) {
                var count = randomSpellEffect.m_effectList.Count;
                var randomEffectIndex = new Random().Next(0, count);
                effect = randomSpellEffect.m_effectList[randomEffectIndex];

                // Push the random effect choice onto the stack.
                effectStack.PushRandomEffectChoice(randomEffectIndex);
            }

            ApplyEffect(effect, action.SpellCaster, action.TargetSubcircle);
        }

        return new CombatAction {
            m_effectChosen = effectStack.GetStackAsUint(),
            m_spellCaster = action.SpellCaster.SlotIndex,
            m_targetSubcircleList = new List<int> { action.TargetSubcircle.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 1, // Determines spell fizzel. 0 = fizzel, >=1 = hit
            m_spell = action.Spell,
        };
    }

    private void ApplyEffect(SpellEffect effect, DuelActorSubCircle caster, DuelActorSubCircle target) {
        var effectTarget = effect.m_effectTarget;

        if (effectTarget == SpellEffect.kEffectTarget.kEnemySingle
         || effectTarget == SpellEffect.kEffectTarget.kFriendlySingle) {
            ApplyEffectSingle(effect, target);
        }
    }

    private void ApplyEffectSingle(SpellEffect effect, DuelActorSubCircle target)
    {
        var effectType = effect.m_effectType;

        switch (effectType)
        {
            case SpellEffect.kSpellEffects.kDamage:
                ApplyEffectDamage(effect, new[] { target });
                break;
            default:
                break;
        }
    }

    private void ApplyEffectDamage(SpellEffect effect, DuelActorSubCircle[] targets) {
        var damage = effect.m_effectParam;

        foreach (var target in targets) {
            target.ParticipantGameStats.m_currentHitpoints -= damage;
        }
    }
}
