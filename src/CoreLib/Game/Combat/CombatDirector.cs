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

public class CombatDirector {
    private readonly Duel _duel;

    private DuelActorSubCircle[] _subCircles = new DuelActorSubCircle[8];
    private DuelActorSubCircle[] ActiveSubCircles => _subCircles.Where(x => x.Occupied).ToArray();
    private bool _awaitingCombatMoves;
    private CombatActionListObj _combatActionList;

    // ctor
    public CombatDirector(Duel duel, DuelActorSubCircle[] actorSubCircles) {
        _duel = duel;
        _subCircles = actorSubCircles;
        _duel.m_firstTeamToAct = (int) DetermineFirstTeam();
    }

    public void StartRound() {
        // Reset the rounds combat action list.
        _awaitingCombatMoves = true;
        _combatActionList = new CombatActionListObj { m_actionList = new List<CombatAction>(), };

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

    public void EndRound() {
        _awaitingCombatMoves = false;
        _combatActionList = null;
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

    public CombatActionListObj GetCombatActionList() {
        return _combatActionList;
    }

    public void AddCombatMove(DuelActorSubCircle caster, DuelActorSubCircle target, Spell spell) {
        if (!_awaitingCombatMoves) {
            throw new InvalidOperationException("Combat moves are not being accepted at this time.");
        }

        var combatAction = new CombatAction {
            m_effectChosen = 4294967284,
            m_spellCaster = caster.SlotIndex,
            m_targetSubcircleList = new List<int> { target.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 1,
            m_spell = spell,
        };
        var combatAction2 = new CombatAction {
            m_effectChosen = 4294967282,
            m_spellCaster = target.SlotIndex,
            m_targetSubcircleList = new List<int> { caster.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 255,
        };
        _combatActionList.m_actionList.Add(combatAction);
        _combatActionList.m_actionList.Add(combatAction2);
    }

    private Team DetermineFirstTeam() {
        // Flip a coin.
        var random = new Random();
        var result = random.Next(0, 2);
        return (Team)result;
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
}
