/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

public class CombatDirector {
    private readonly Duel _duel;

    private bool _awaitingCombatMoves;
    private CombatActionListObj _combatActionList;

    // ctor
    public CombatDirector(Duel duel) {
        _duel = duel;
    }

    public void StartCombat() {
        _duel.m_firstTeamToAct = (int) DetermineFirstTeam();
    }

    public void StartRound() {
        _awaitingCombatMoves = true;
        _combatActionList = new CombatActionListObj {
            m_actionList = new List<CombatAction>(),
        };
    }

    public void EndRound() {
        _awaitingCombatMoves = false;
        _combatActionList = null;
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

    public CombatActionListObj GetCombatActionList() {
        return _combatActionList;
    }

    private Team DetermineFirstTeam() {
        // Flip a coin.
        var random = new Random();
        var result = random.Next(0, 2);
        return (Team)result;
    }
}
