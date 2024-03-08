/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty.PropertyReflection;
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
}

public class CombatDirector {
    private readonly Duel _duel;

    private CombatDuelActorSubCircle[] _subCircles = new CombatDuelActorSubCircle[8];
    private CombatDuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();
    private bool _awaitingCombatMoves;
    private List<QueuedCombatAction> _queuedCombatActions;

    // ctor
    public CombatDirector(Duel duel, CombatDuelActorSubCircle[] actorSubCircles) {
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
            if (!circle.AddedToDuel) {
                return;
            }

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

    public uint GetQueuedCombatActionsTime() {
        var count = 0;

        // Every spell takes 10 seconds.
        foreach (var action in _queuedCombatActions) {
            if (action.Spell != null) {
                count += 10;
            }
        }

        // Every pass takes 1 seconds.
        count += (ActiveSubCircles.Where(x => x.IsAlive).Count() - _queuedCombatActions.Count) * 1;

        return (uint) count;
    }

    public CombatActionListObj ApplyQueuedCombatActions() {
        var combatActionList = new CombatActionListObj { m_actionList = new List<CombatAction>() };

        // Any caster that has not queued an action will pass their turn.
        foreach (var subCircle in ActiveSubCircles) {
            if (!subCircle.AddedToDuel || !subCircle.IsAlive) {
                continue;
            }

            if (!_queuedCombatActions.Any(x => x.SpellCaster == subCircle)) {
                var queuedAction = new QueuedCombatAction {
                    SpellCaster = subCircle,
                    TargetSubcircle = subCircle,
                    Spell = null,
                };
                _queuedCombatActions.Add(queuedAction);
            }
        }

        // Sort the queued actions by the caster's slot index. We'll also want to take into account
        // the team that's going first.
        _queuedCombatActions.Sort((a, b) =>
        {
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

        // Iterate through each queued combat action and apply the spell effects.
        foreach (var action in _queuedCombatActions) {
            var combatAction = CombatEffectApplicator.ApplyCombatAction(action);
            combatActionList.m_actionList.Add(combatAction);

            if (action.Spell is null) {
                continue;
            }

            var caster = action.SpellCaster.CombatParticipant;
            var spell = action.Spell;

            // Remove the caster's pips.
            caster.m_pipCount.m_genericPips -= spell.m_pipCost.m_spellRank;
        }

        // Log the combat actions.
        Logger.Debug("Duel {0} | Combat actions round {1}: ", Logger.Args(_duel.m_duelID, _duel.m_roundNum));
        foreach (var action in combatActionList.m_actionList)
        {
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

        return combatActionList;
    }

    public void EndRound() {
        _awaitingCombatMoves = false;
        _queuedCombatActions = null;
    }

    public CombatPipListObj GetCombatParticipantsPips() {
        var pips = new CombatPipListObj { m_pipList = new List<ParticipantPipData>() };

        EnactActionOnSubCircles(circle => {
            if (!circle.AddedToDuel || !circle.IsAlive) {
                return;
            }

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
            if (!circle.AddedToDuel || !circle.IsAlive) {
                return;
            }

            var participantHealth = new ParticipantParameter {
                m_data = (uint) circle.ParticipantGameStats.m_currentHitpoints,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
            };
            healthList.m_healthList.Add(participantHealth);
        });

        return healthList;
    }

    public void AddCombatMove(CombatMoveType type, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target, Spell spell) {
        if (!_awaitingCombatMoves) {
            throw new InvalidOperationException("Combat moves are not being accepted at this time.");
        }
        if (!caster.AddedToDuel || !target.AddedToDuel) {
            throw new InvalidOperationException("Both the caster and target must be added to the duel.");
        }
        if (!caster.IsAlive) {
            throw new InvalidOperationException("The caster must be alive.");
        }

        // If this spell is already queued by the same caster, remove all of their queued actions.
        _queuedCombatActions.RemoveAll(x => x.SpellCaster == caster);

        if (!spell.m_pipCost.m_xPipSpell && caster.CombatParticipant.m_pipCount.m_genericPips < spell.m_pipCost.m_spellRank) {
            Logger.Error("Duel {0} | Slot {1} | Not enough pips to cast spell {2}",
                Logger.Args(_duel.m_duelID, caster.SlotIndex, spell.m_templateID));
            return;
        }

        var queuedAction = new QueuedCombatAction {
            SpellCaster = caster,
            TargetSubcircle = target,
            Spell = type == CombatMoveType.Attack ? spell : null,
        };
        _queuedCombatActions.Add(queuedAction);
    }

    private CombatTeam DetermineFirstTeam() {
        // Flip a coin.
        var random = new Random();
        var result = random.Next(0, 2);
        return (CombatTeam) result;
    }

    private bool DeterminePowerPipGain(CombatParticipant participant) {
        var powerPipProbability = participant.m_pGameStats.m_powerPipBase;
        var powerPipChance = new Random().Next(0, 100);
        return powerPipChance <= powerPipProbability;
    }

    private void EnactActionOnSubCircles(Action<CombatDuelActorSubCircle> action) {
        foreach (var subCircle in ActiveSubCircles) {
            action(subCircle);
        }
    }
}
