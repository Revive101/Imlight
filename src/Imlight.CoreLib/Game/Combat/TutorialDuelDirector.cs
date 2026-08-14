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
 * COMBAT DUEL SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Scripts the tutorial golem duels: applies each round's quest-goal grants
 * (hands, pips) and pre-queues the golems' attacks so the fight resolves
 * without creature AI. Owns all tutorial-specific duel behavior.
 * 
 * USAGE EXAMPLE:
 * Constructed by CombatDuelComponent at startup. The component calls the
 * On* lifecycle hooks from the duel phases and delegates the TUTORIAL_108
 * protocol handlers here.
 * 
 * NOTE:
 * Runs on the duel component's actor thread, a plain helper like
 * CombatResolver rather than an actor. Activates only when the zone path
 * contains "Tutorial".
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 08/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Scripts tutorial duels on behalf of the duel component: round-goal grants,
/// queued hand reveals, and the golems' scripted moves.
/// </summary>
internal sealed class TutorialDuelDirector {

    private static readonly Dictionary<int, string[]> s_roundGoals = new() {
        [2] = ["mob damage 2", "mob damage 3"],
        [3] = ["mob damage 4", "player heal"],
        [4] = ["mob weakness", "player damage 2", "Give 3 pips to player"],
        [5] = ["mob damage 5", "mob damage 6", "player blade"],
        [6] = ["mob damage 7", "mob damage 8", "player damage 3", "give 4 pips to player"],
    };

    private readonly CombatDuelComponent _duel;
    private readonly bool _isActive;
    private uint[] _pendingHandGrant;

    internal TutorialDuelDirector(CombatDuelComponent duel, string zonePath) {
        _duel = duel;
        _isActive = zonePath.Contains("Tutorial", StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsActive => _isActive;

    internal void OnDuelCreated(Duel duel) {
        if (!_isActive) {
            return;
        }

        // Tutorial fights are client-paced (no planning countdown) and the golems act first (retail pacing).
        duel.m_tutorialMode = true;
        duel.m_disableTimer = true;
        duel.m_firstTeamToAct = (int) CombatTeam.Monster;
    }

    internal void OnNewRound(int roundNum) {
        if (!_isActive) {
            return;
        }

        // Execute the round's quest-goal grants (rounds 2-6; round 1 is client-driven) so the golems' hands
        // and the player's cards/pips are set before planning begins.
        GrantRoundGrants(roundNum);

        // Pre-queue the golems' scripted attacks; they never pick moves on their own and without this the
        // round can't complete.
        ScriptEnemyMoves();
    }

    internal void OnPlanningPhaseBegin() {
        if (!_isActive) {
            return;
        }

        FlushPendingGrant();
        ScriptEnemyMoves();
    }

    internal void OnCombatMoveQueued() {
        if (!_isActive) {
            return;
        }

        // Round-1 grants land during planning, after the planning-begin script ran; enqueue the golems'
        // moves now so the round resolves with them (AddCombatMove replaces the caster's queued action,
        // so this is idempotent with the planning-begin pass).
        ScriptEnemyMoves();
    }

    internal void OnDuelEnded()
        => _pendingHandGrant = null;

    internal void ReceiveRebuildDuelHand(IActorRef sender, TUTORIAL_108_PROTOCOL.MSG_TUTORIALREBUILDDUELHAND message) {
        if (!_isActive || message.SpellIdsToGrant is null || message.SpellIdsToGrant.Length == 0) {
            return;
        }

        var recipient = _duel.SubCircles.FirstOrDefault(x => x.ParticipantActor == sender);
        if (message.RecipientTemplateId != 1) {
            recipient = _duel.SubCircles.FirstOrDefault(x => x is not null && x.ParticipantObject is not null
                && x.ParticipantObject.m_templateID == message.RecipientTemplateId);
        }
        if (recipient is null) {
            Logger.Warning("Duel {0} | Tutorial hand grant for template {1} found no participant.",
                Logger.Args(_duel.Duel.m_duelID.Full, message.RecipientTemplateId));

            return;
        }

        // Player grants that land mid-execution are queued to the next planning phase so the card does not
        // flash during the current cast; creature grants apply now (the round script reads them at planning).
        if (message.RecipientTemplateId == 1 && _duel.Duel.m_duelPhase != kDuelPhase.kPhase_Planning) {
            _pendingHandGrant = [.. _pendingHandGrant, .. message.SpellIdsToGrant];

            return;
        }

        recipient.AddSpellsToHand(message.SpellIdsToGrant);
        _duel.SendCurrentCombatHand(recipient);
    }

    internal void ReceiveGrantPips(IActorRef sender, TUTORIAL_108_PROTOCOL.MSG_TUTORIALGRANTPIPS message) {
        if (!_isActive) {
            return;
        }

        var caster = _duel.SubCircles.FirstOrDefault(x => x.ParticipantActor == sender);
        if (caster is null) {
            Logger.Warning("Duel {0} | Tutorial pip grant from an actor that is not in the duel.",
                Logger.Args(_duel.Duel.m_duelID.Full));

            return;
        }

        caster.SetPips(message.Count);
        _duel.SendCombatPips();
    }

    private void GrantRoundGrants(int roundNum) {
        if (!s_roundGoals.TryGetValue(roundNum, out var goalNames)) {
            return;
        }

        var quest = QuestTemplateCollection.GetQuestByName("WC-TUT-C03-001");
        if (quest is null) {
            return;
        }

        var player = GetPlayerSubCircle();

        foreach (var goalName in goalNames) {
            var goal = quest.m_goals.FirstOrDefault(g => g.m_goalName == goalName);
            if (goal?.m_completeResults?.m_results is null) {
                continue;
            }

            foreach (var result in goal.m_completeResults.m_results) {
                if (result is ResGiveSpell give) {
                    var recipientTemplate = give.m_spellID != 0 ? give.m_templateID : 1;
                    var spellId = give.m_spellID != 0 ? give.m_spellID : give.m_templateID;
                    var recipient = recipientTemplate == 1
                        ? player
                        : _duel.SubCircles.FirstOrDefault(s => s is not null && s.ParticipantObject is not null
                            && s.ParticipantObject.m_templateID == recipientTemplate);
                    recipient?.AddSpellsToHand([(uint) spellId]);
                }
                else if (result is ResDrawHand draw && draw.m_templateID != 0
                         && CoreObjectFactory.GetCoreTemplate(draw.m_templateID) is SpellTemplate) {
                    player?.AddSpellsToHand([(uint) draw.m_templateID]);
                }
            }

            if (goal.m_tallyCounter?.m_count > 0 && player is not null) {
                player.SetPips(goal.m_tallyCounter.m_count);
                _duel.SendCombatPips();
            }
        }

        if (player is not null) {
            _duel.SendCurrentCombatHand(player);
        }
    }

    private void ScriptEnemyMoves() {
        if (_duel.CombatResolver is null) {
            return;
        }

        var player = GetPlayerSubCircle();
        if (player is null) {
            return;
        }

        foreach (var enemy in _duel.SubCircles
                     .Where(s => s is not null && s.AddedToDuel && s.IsAlive && s.OccupiedTeam == CombatTeam.Monster)
                     .OrderBy(s => s.SlotIndex)) {
            var queued = _duel.CombatResolver.GetQueuedAction(enemy);
            if (queued?.Spell is not null) {
                continue;
            }

            var spell = enemy.GetSpellFromLastHand(0);
            if (spell is not null) {
                _duel.CombatResolver.AddCombatMove(CombatMoveType.Attack, enemy, player, spell);
                enemy.ClearHand();
                Logger.Information("[TUTFIGHT] round {0}: enemy slot {1} scripted to cast '{2}' at the player.",
                    Logger.Args(_duel.Duel.m_roundNum, enemy.SlotIndex, spell.m_templateID));
            }
            else if (queued is null) {
                _duel.CombatResolver.AddCombatMove(CombatMoveType.Pass, enemy, null, null);
            }
        }
    }

    private void FlushPendingGrant() {
        if (_pendingHandGrant is not { Length: > 0 }) {
            return;
        }

        // Reveal at the start of this planning phase, not the instant the client fired the goal: mid-execution
        // an instant reveal would flash next round's card during the cast.
        var pendingPlayer = GetPlayerSubCircle();
        if (pendingPlayer is not null) {
            pendingPlayer.AddSpellsToHand(_pendingHandGrant);
            _duel.SendCurrentCombatHand(pendingPlayer);
        }
        _pendingHandGrant = null;
    }

    private CombatDuelSubCircle GetPlayerSubCircle()
        => _duel.SubCircles.FirstOrDefault(s => s is not null && s.AddedToDuel && s.IsAlive
                                                && s.OccupiedTeam == CombatTeam.Player);
}
