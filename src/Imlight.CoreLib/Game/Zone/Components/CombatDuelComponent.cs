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
 * Manages turn-based combat encounters between players and creatures.
 * This component is responsible for handling duel initiation, combat moves,
 * and duel resolution. It also manages the duel state and participant data.
 * 
 * USAGE EXAMPLE:
 * This component may activate from an NpcComponent on a dueling creature.
 * It may also activate as a result of `ResStartDuel` from a trigger.
 * 
 * NOTE:
 * This class is merely the director of the duel. The actual combat logic is
 * all handled within the `Imlight.CoreLib.Game.Combat` namespace.
 * Start at the `CombatResolver` class and work your way down.
 * Tutorial duels are scripted by the `TutorialDuelDirector` in that namespace.
 * Combat positions are determined by sigil templates, with specific subcircle positions.
 * 
 * TODO:
 * - Implementation of creature stunning functionality
 * - Calculate the correct damage percent max as a limit function
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.Math;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Manages combat duels between players and creatures in the game world.
/// Controls the full duel lifecycle including initialization, participant management,
/// turn sequencing, spell casting, and resolution of combat.
/// </summary>
/// <remarks>
/// Acts as the central orchestrator for the combat system, handling messaging between
/// all participants, managing combat state transitions, and coordinating the various
/// phases of combat (planning, execution, resolution). The component creates and 
/// positions subcircles according to sigil templates and maintains combat state
/// including team assignments, turn order, and participant status.
/// </remarks>
internal sealed class CombatDuelComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IComponentFactory, IWithTimers, IClientBehaviorProvider<WizardClientDuelBehavior> {

    private const byte PLANNING_TIME = 30;
    private const float DUEL_GRACE_PERIOD_IN_SECONDS = 3.75f;
    private const float DUEL_NEW_ROUND_DELAY = 1.86f;
    private const float YAW_ERROR_COMPENSATION = 1.58f;
    private const string GRACE_TIME_KEY = "GracePeriod";
    private const string PLANNING_TIME_KEY = "PlanningPhase";
    private const string PREPLANNING_TIME_KEY = "PrePlanningPhase";
    private const string RESOUTION_TIME_KEY = "ResolutionPhase";
    private const double MINION_SUMMON_ANIMATION_DELAY = 5.5;
    private const string GROUP_JOIN_PROMPT_KEY = "GUI_GotoFriendQuestion";
    private const string PVP_RETURN_GRACE_KEY = "PvPReturnGrace";
    private const double PVP_RETURN_GRACE_SECONDS = 0;
    private const int TEAM_SLOT_COUNT = 4;
    private const int DEFAULT_BASE_ENEMY_COUNT = 2;
    private const int ONE_ENEMY_PER_PLAYER_MARKER = -1;

    public bool NoTransfer { get; set; } = false;
    public ITimerScheduler Timers { get; set; }
    public Duel Duel { get; private set; }
    public Combat.CombatResolver CombatResolver { get; private set; }
    public CombatDuelSubCircle[] SubCircles { get; private set; }
    public CombatDuelSubCircle[] ActiveSubCircles => [.. SubCircles.Where(x => x.Occupied)];
    // Human players only; a minion must not let an extra enemy scale into the fight.
    public byte PlayerCount => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player
                                                            && !x.IsSummonedMinion);
    public byte CreatureCount => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster);
    // Exclude minions from the loss condition; they must not hold a fight open.
    public byte AlivePlayerCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.IsAlive
                                        && !x.IsSummonedMinion);
    public byte AliveCreatureCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.IsAlive);
    public byte PlayersInDuel
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.AddedToDuel);
    public byte CreaturesInDuel
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.AddedToDuel);
    public byte AliveAndInDuelPlayerCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.IsAlive && x.AddedToDuel
                                        && !x.IsSummonedMinion);
    public byte AliveAndInDuelCreatureCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.IsAlive && x.AddedToDuel);
    public ulong SigilId => Entity.ActiveGameObject.m_globalID;
    internal CombatSigilTemplate SigilTemplate => _sigilTemplate;
    internal bool IsPvPDuel => _pvp is not null;
    internal uint PvPLeagueId => _pvp?.LeagueId ?? 0;
    private byte PlanningSeconds => IsPvPDuel ? PvPSettings.PracticePlanningSeconds : PLANNING_TIME;

    /// <summary>
    /// Counts the wizards of one side that fight on, whichever side that is.
    /// </summary>
    public byte AliveHumanCount(CombatTeam team)
        => (byte) SubCircles.Count(x => x.Occupied && x.IsHumanPlayer && x.OccupiedTeam == team && x.IsAlive);

    // Keyed by the actor: a CoreObject is a record whose hash follows its location.
    private readonly HashSet<IActorRef> _entitiesInRange = [];
    private readonly CombatGroupReservations _groupReservations = new();
    private readonly ObjectSerializer _serializer = new(
        Versionable: false,
        Behaviors: SerializerFlags.None
    );
    private readonly PropertyFlags _combatParticipantFlags = (PropertyFlags) 4;
    private readonly PropertyFlags _combatParticipantStatFlags = (PropertyFlags) 5;
    private readonly PropertyFlags _combatParticipantHandFlags = (PropertyFlags) 5;
    private readonly PropertyFlags _upFirstFlags = PropertyFlags.Prop_Transmit
                                                 | PropertyFlags.Prop_AuthorityTransmit
                                                 | PropertyFlags.Prop_Public;

    private CombatSigilObjectInfo _combatSigilObjectInfo;
    private RenderComponent _renderComponent;
    private CombatSigilTemplate _sigilTemplate;
    private PvPDuelState _pvp;
    private bool _isActive;
    private bool _awaitingCombatMoves;
    private TutorialDuelDirector _tutorialDirector;
    // Seconds of cinematics before a caster's cast; SummonMinion adds the animation delay to it.
    internal float CurrentActionCinematicOffsetSeconds;
    internal bool CheatInstantCinematics { get; set; }

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is not null && x.m_behaviorName == "DuelBehavior");

    public override void OnStart() {
        // Disable the RenderComponent. We'll activate it when the sigil is activated.
        _renderComponent = Entity.GetComponentOfType<RenderComponent>();
        _renderComponent?.Disable();

        _tutorialDirector = new TutorialDuelDirector(this, Entity.Zone?.ZonePath ?? "");
    }

    public WizardClientDuelBehavior GetClientBehaviorInstance() => new() {
        m_pDuel = Duel,
        // todo: this is the sigil template itself, not for the game object
        m_sigilTemplateID = IsPvPDuel ? StringHash.Compute(_sigilTemplate.m_sigilName) : 1901671683,
    };

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!_isActive) {
            return;
        }

        // Check if this player is in the duel. If they are, remove them from the duel.
        var subCircle = SubCircles.FirstOrDefault(x => x is not null && x.ParticipantActor == playerActor);
        if (subCircle is null) {
            return;
        }

        // Handle this as if it were the flee action.
        HandleFleeAction(subCircle);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        // The roster of a PvP duel is fixed; nobody joins by walking in.
        if (!_isActive || IsPvPDuel) {
            return;
        }

        // Check if the player is now in range of the object.
        // If there's a slot available, add the player to the duel.
        if (IsInRadius(playerObj, _combatSigilObjectInfo.m_radius) && !_entitiesInRange.Contains(playerActor)) {
            _entitiesInRange.Add(playerActor);

            if (IsSlotAvailable(CombatTeam.Player)) {
                AddParticipant(playerObj, playerActor);
            }
        }
        else if (!IsInRadius(playerObj, _combatSigilObjectInfo.m_radius) && _entitiesInRange.Contains(playerActor)) {
            _entitiesInRange.Remove(playerActor);
        }
    }

    public override void OnCreatureMove(CoreObject creature, IActorRef suspect, ZoneEntity entity) {
        if (!_isActive || IsPvPDuel) {
            return;
        }

        // Check if the creature is now in range of the object.
        // If there's a slot available, add the creature to the duel.
        if (IsInRadius(creature, _combatSigilObjectInfo.m_radius) && !_entitiesInRange.Contains(suspect)) {
            _entitiesInRange.Add(suspect);

            // The creature that started the fight, or one that joined and walked back in, already holds a circle.
            if (IsParticipant(suspect)) {
                return;
            }

            var npcComponent = entity.GetComponentOfType<NpcComponent>();
            if (npcComponent != null && !npcComponent.IsMonster) {
                return;
            }

            if (IsSlotAvailable(CombatTeam.Monster)) {
                AddParticipant(creature, suspect);
            }
            else {
                // Delete the creature.
                var deleteMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEATH();
                suspect.Tell(deleteMsg);
            }
        }
        else if (!IsInRadius(creature, _combatSigilObjectInfo.m_radius) && _entitiesInRange.Contains(suspect)) {
            _entitiesInRange.Remove(suspect);
        }
    }

    private const string MONSTER_KILLED_EVENT = "Monster_Killed";

    internal void ZoneBroadcast(IMessage message) => Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
        Selfless = false,
        Sender = Self,
        Message = message
    });

    internal void CreatureBroadcast(IMessage message) => EnactActionOnSubCircles(circle => {
        if (circle.OccupiedTeam == CombatTeam.Monster) {
            circle.ParticipantActor.Tell(message);
        }
    });

    // Client packets go to the players only: creatures and summoned minions are server entities that read none.
    internal new void PlayerBroadcast(IMessage message) => EnactActionOnSubCircles(circle => {
        if (circle.OccupiedTeam == CombatTeam.Player && !circle.IsSummonedMinion) {
            circle.ParticipantActor.Tell(message);
        }
    });

    /// <summary>
    /// Sends the message to everyone with a client: the wizards of either side and their minions, not the
    /// creatures of the AI.
    /// </summary>
    internal void HumanBroadcast(IMessage message) => EnactActionOnSubCircles(circle => {
        if (!circle.IsAiCreature) {
            circle.ParticipantActor.Tell(message);
        }
    });

    internal void TeamBroadcast(CombatTeam team, IMessage message) => EnactActionOnSubCircles(circle => {
        if (circle.OccupiedTeam == team) {
            circle.ParticipantActor.Tell(message);
        }
    });

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SIGILDETAILS))]
    private void ReceiveSigilDetails(ZONE_102_PROTOCOL.MSG_SIGILDETAILS message) {
        _combatSigilObjectInfo = message.CombatSigilObjectInfo;

        // Get the sigil template.
        _sigilTemplate = (CombatSigilTemplate) SigilFactory.GetSigilTemplate(_combatSigilObjectInfo.m_sigilType);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveDuelStart(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        if (_isActive) {
            return;
        }

        if (_renderComponent is null) {
            Logger.Error("RenderComponent is null for duel {0}! Deleting sigil.",
                Logger.Args(SigilId));

            Entity.DespawnObject();

            return;
        }

        InitializeDuel(message.StartingParticipants);
        ActivateSigil();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_STARTPVPMATCH))]
    private void ReceiveStartPvPMatch(ZONE_102_PROTOCOL.MSG_STARTPVPMATCH message) {
        if (_isActive) {
            Logger.Warning("Match {MatchId} cannot start on sigil {SigilId}: a duel is already running.",
                Logger.Args(message.MatchId, SigilId));

            return;
        }

        if (_sigilTemplate is not PvPCombatSigilTemplate || _renderComponent is null) {
            Logger.Error("Sigil {SigilId} cannot host PvP match {MatchId}.", Logger.Args(SigilId, message.MatchId));
            CancelPvPMatch(message.MatchId, PvPCancelReason.NoArena);

            return;
        }

        if (!PvPTournamentFactory.TryGetTournament(message.TournamentNameId, out var tournament)
            || !PvPGameTypes.TryResolve(tournament.m_pvpDuelModifiers.Select(name => (string) name), out var rules)) {
            Logger.Error("Match {MatchId} has rules the duel does not support.", Logger.Args(message.MatchId));
            CancelPvPMatch(message.MatchId, PvPCancelReason.UnsupportedRules);

            return;
        }

        try {
            InitializePvPDuel(message, rules);
        } catch (Exception ex) {
            Logger.Error("Match {MatchId} could not seat its players: {Exception}", Logger.Args(message.MatchId, ex));
            AbortPvPDuel(message.MatchId, PvPCancelReason.NoArena);

            return;
        }

        ActivateSigil();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FORFEITPVPPARTICIPANT))]
    private void ReceiveForfeitPvPParticipant(ZONE_102_PROTOCOL.MSG_FORFEITPVPPARTICIPANT message) {
        if (!IsPvPDuel || !_isActive || _pvp.MatchId != message.MatchId) {
            return;
        }

        var subCircle = SubCircles.FirstOrDefault(circle => circle.Occupied && circle._wizard?.CharId == message.CharId);
        if (subCircle is null) {
            return;
        }

        ForfeitPvPDuel(subCircle);
    }

    private void ActivateSigil() {
        _renderComponent.Enable();

        // Broadcast MSG_DUEL to inform all clients a duel is now active.
        // The live server sends this to enable 3D combat targeting.
        BroadcastDuel();

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DUEL_GRACE_PERIOD_IN_SECONDS);
        Timers.StartSingleTimer(GRACE_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_NEWROUND(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        if (!_isActive) {
            return;
        }

        Logger.Debug("Duel {0} | New round {1} at {2}",
            Logger.Args(Duel.m_duelID.Full, Duel.m_roundNum, DateTime.Now.ToString("HH:mm:ss")));

        _groupReservations.DropStale(ZoneActor, SubCircles);
        if (Duel.m_roundNum == 0) {
            ApplyFullGroupInitiative();
        }

        if (IsPvPDuel) {
            _pvp.BeginRound(Duel.m_roundNum + 1);
            Duel.m_firstTeamToAct = (int) _pvp.RoundStarter;
        }

        // Add the circles to combat if they are not already.
        AddWaitingCombatParticipants();
        if (Duel.m_roundNum <= 1) {
            LogStartingPips();
        }

        CombatResolver.Reset();
        _awaitingCombatMoves = true;

        // Determine the power pip gain. Alternating turns gain in the opponent's phase: the non-starter now, except in
        // the first round, where both teams gain so the starter has a pip to act with.
        DoPipGain(IsAlternatingPvP && Duel.m_roundNum > 0 ? PvPDuelState.Opposite(_pvp.RoundStarter) : null);

        if (IsPvPDuel) {
            BeginPvPHalfTurn();
        } else {
            // Echo the new round message to all actors.
            EnactActionOnSubCircles(circle => circle.ParticipantActor.Tell(message));
        }

        // Pre-planning phase just wants to send who is up first.
        Duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;
        Duel.m_roundNum++;
        SendCombatPhase((byte) Duel.m_duelPhase);
        SendUpFirst(Duel.m_roundNum);

        _tutorialDirector.OnNewRound(Duel.m_roundNum);

        var delay = TimeSpan.FromSeconds(DUEL_NEW_ROUND_DELAY);
        Timers.StartSingleTimer(PREPLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEBEGIN(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEBEGIN))]
    private void ReceivePlanningPhaseBegin() {
        // Planning phase is when each participant notices their new stats and "plans" accordingly.
        Duel.m_duelPhase = kDuelPhase.kPhase_Planning;
        SendCombatPhase((byte) Duel.m_duelPhase);

        SendCombatStats();
        SendCombatHand();
        SendCombatPips();
        SendCombatHealth();
        SendCombatUI(PlanningSeconds);

        // Re-telegraph minion AI moves now that the planning HUD exists.
        ResendMinionMoveSelections();

        // Tutorial duels flush queued card grants and re-script the golems before planning.
        _tutorialDirector.OnPlanningPhaseBegin();

        // Tutorial fights have no planning countdown (m_disableTimer): the client drives pacing. Planning still
        // ends normally once every participant has enqueued a move (ReceiveCombatMove).
        if (!_tutorialDirector.IsActive) {
            var delay = TimeSpan.FromSeconds(PlanningSeconds);
            Timers.StartSingleTimer(PLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER(), delay);
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATDRAW))]
    private void ReceiveCombatDraw(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATDRAW message) {
        // Find which sub circle this draw request is for.
        var caster = SubCircles.FirstOrDefault(x => x.ParticipantActor == message.Actor);
        if (caster is null) {
            Logger.Warning("Duel {0} | Combat draw received from an actor that is not in the duel.",
                Logger.Args(Duel.m_duelID.Full));

            return;
        }

        // Draw a random treasure card from the vault.
        var drawnSpell = caster.DrawFromVault();
        if (drawnSpell != null) {
            Logger.Debug("Duel {0} | Slot {1} | Drew treasure card {2} from vault.",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, drawnSpell.m_templateID));
        }

        // Send the hand as-is: a refilling send would also pull real deck cards into the open slots.
        SendCurrentCombatHand(caster);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORSETARCHMASTERYSCHOOL))]
    private void ReceiveSetArchmasterySchool(COMBAT_106_PROTOCOL.MSG_ACTORSETARCHMASTERYSCHOOL message) {
        var circle = SubCircles.FirstOrDefault(x => x.ParticipantActor == message.Actor);
        if (circle is null || !CombatArchmastery.SelectSchool(circle, message.School)) {
            return;
        }

        SendCombatPips();
    }

    [MessageHandler(typeof(TUTORIAL_108_PROTOCOL.MSG_TUTORIALREBUILDDUELHAND))]
    private void ReceiveTutorialRebuildDuelHand(TUTORIAL_108_PROTOCOL.MSG_TUTORIALREBUILDDUELHAND message) {
        if (!_isActive) {
            return;
        }

        _tutorialDirector.ReceiveRebuildDuelHand(Sender, message);
    }

    [MessageHandler(typeof(TUTORIAL_108_PROTOCOL.MSG_TUTORIALGRANTPIPS))]
    private void ReceiveTutorialGrantPips(TUTORIAL_108_PROTOCOL.MSG_TUTORIALGRANTPIPS message) {
        if (!_isActive) {
            return;
        }

        _tutorialDirector.ReceiveGrantPips(Sender, message);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE))]
    private void ReceiveCombatMove(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE message) {
        if (!_isActive) {
            return;
        }

        // Find which sub circle this is.
        var caster = SubCircles.FirstOrDefault(x => x.ParticipantActor == message.Actor)
            ?? throw new Exception("Combat move received from an actor that is not in the duel.");

        // Tutorial duels script the golems' moves server-side; drop their own AI moves so a pass cannot
        // overwrite the scripted attack. Player moves still flow through normally.
        if (_tutorialDirector.IsActive && caster.OccupiedTeam == CombatTeam.Monster) {
            return;
        }

        if (!_awaitingCombatMoves) {
            Logger.Warning("Duel {0} | Slot {1} | Received combat move while not expecting it.",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex));

            return;
        }

        if (IsPvPDuel && !IsPvPMoveAllowed(caster)) {
            return;
        }

        var moveType = (CombatMoveType) message.MoveType;

        switch (moveType) {
            case CombatMoveType.Discard:
                HandleDiscardMove(caster, message.SpellSelection);
                break;
            case CombatMoveType.Pass:
                HandlePassMove(caster);
                break;
            case CombatMoveType.Attack:
                HandleAttackMove(caster, message.SpellSelection, message.SpellTarget);
                break;
            case CombatMoveType.Flee:
                HandleFleeAction(caster);
                break;
            case CombatMoveType.ChangeMind:
                HandleChangeMindAction(caster);
                break;
            default:
                Logger.Warning("Duel {0} | Slot {1} | Invalid combat move type: {2}",
                    Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, moveType));
                break;
        }

        _tutorialDirector.OnCombatMoveQueued();

        // If by this point all participants have inputted their moves, we can start the next phase.
        var participantCount = AlivePlayerCount + AliveCreatureCount;
        if (CombatResolver.HaveAllParticipantsEnqueuedActions()) {
            _awaitingCombatMoves = false;

            // Adding a new timer will cancel the old one.
            var delay = TimeSpan.FromSeconds(1);
            Timers.StartSingleTimer(PLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER(), delay);
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER))]
    private void ReceivePlanningPhaseOver(COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER message) {
        if (!_isActive) {
            return;
        }

        // A repeated timer must not execute a half turn twice.
        if (IsPvPDuel && Duel.m_duelPhase != kDuelPhase.kPhase_Planning) {
            return;
        }

        Logger.Debug("Duel {0} | Round {1} over at {2}",
            Logger.Args(Duel.m_duelID.Full, Duel.m_roundNum, DateTime.Now.ToString("HH:mm:ss")));

        if (IsPvPDuel) {
            try {
                ExecutePlanningPhaseOver();
            }
            catch (Exception exception) {
                Logger.Error("Duel {0} | Round {1} | Resolving the half turn failed, skipping it: {2}",
                    Logger.Args(Duel.m_duelID.Full, Duel.m_roundNum, exception));
                SkipFailedPvPHalfTurn();
            }

            return;
        }

        ExecutePlanningPhaseOver();
    }

    private void SkipFailedPvPHalfTurn() {
        _awaitingCombatMoves = false;
        Duel.m_duelPhase = kDuelPhase.kPhase_Resolution;
        SendCombatPhase((byte) Duel.m_duelPhase);
        ContinuePvPDuel();
    }

    private void ExecutePlanningPhaseOver() {
        // The execution phase begins. This is where combat actions take place and we actually see spell cinematics.
        _awaitingCombatMoves = false;
        Duel.m_duelPhase = kDuelPhase.kPhase_Execution;
        SendCombatPhase((byte) Duel.m_duelPhase);

        // Determine how long the cinematics will take.
        var cinematicTimeInSeconds = CombatResolver.ApplyQueuedCombatActions(out var actions);
        var actionExecutionTime = TimeSpan.FromSeconds(cinematicTimeInSeconds);
        Duel.m_executionPhaseTimer = (float) actionExecutionTime.TotalSeconds;

        // Serialize the combat actions and send them to the clients.
        if (!_serializer.Serialize(actions, _combatParticipantHandFlags, out var buffer)) {
            Logger.Error("Failed to serialize combat actions for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATACTIONS {
            DuelID = SigilId,
            ActionData = buffer,
        };
        ZoneBroadcast(msg);

        Timers.StartSingleTimer(RESOUTION_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_ROUNDRESOLUTION(), actionExecutionTime);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ROUNDRESOLUTION))]
    private void ReceiveRoundResolution(COMBAT_106_PROTOCOL.MSG_ROUNDRESOLUTION message) {
        if (!_isActive) {
            return;
        }

        // Iterate through dead creature participants and remove them from the duel.
        // Players can be healed and therefore don't need to be removed.
        EnactActionOnSubCircles(circle => {
            // A dead minion can't be revived; remove it like an enemy.
            if (!circle.IsHumanPlayer && !circle.IsAlive) {
                var removeMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEATH();
                circle.ParticipantActor.Tell(removeMsg);
            }
        });

        // All spells have been called. Inform the client whether this duel continues or ends.
        Duel.m_duelPhase = kDuelPhase.kPhase_Resolution;
        SendCombatPhase((byte) Duel.m_duelPhase);

        if (IsPvPDuel) {
            ContinuePvPDuel();

            return;
        }

        var playersWin = AliveCreatureCount <= 0;
        var creaturesWin = AlivePlayerCount <= 0;
        if (!playersWin && !creaturesWin) {
            // Continue. Start a new round.
            Self.Tell(new COMBAT_106_PROTOCOL.MSG_NEWROUND());

            return;
        }

        EndDuel();
    }

    private void ContinuePvPDuel() {
        if (IsPvPSideWiped()) {
            EndDuel();

            return;
        }

        if (_pvp.TryBeginSecondHalfTurn()) {
            // The other team acts next in the same round. The starter team gains its pips as phase two begins.
            DoPipGain(_pvp.RoundStarter);
            BeginPvPHalfTurn();
            Duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;
            SendCombatPhase((byte) Duel.m_duelPhase);
            SendUpFirst(Duel.m_roundNum);

            var delay = TimeSpan.FromSeconds(DUEL_NEW_ROUND_DELAY);
            Timers.StartSingleTimer(PREPLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEBEGIN(), delay);

            return;
        }

        Self.Tell(new COMBAT_106_PROTOCOL.MSG_NEWROUND());
    }

    private void BeginPvPHalfTurn() {
        // Only the members of the acting team may queue moves, and its minions plan now.
        var acting = _pvp.ActingTeam;
        CombatResolver.Reset();
        CombatResolver.ActingTeam = acting;
        _awaitingCombatMoves = true;

        if (acting is { } actingTeam) {
            Duel.m_firstTeamToAct = (int) actingTeam;
            Duel.m_altTurnCounter = _pvp.HalfTurn;
            Duel.m_originalFirstTeamToAct = (int) _pvp.OriginalFirstTeam;
        }

        EnactActionOnSubCircles(circle => {
            if (acting is { } team) {
                circle.CombatParticipant.m_myTeamTurn = circle.OccupiedTeam == team;
            }

            // The AI of a minion plans when its team may act.
            if (circle.IsSummonedMinion && (acting is null || circle.OccupiedTeam == acting)) {
                circle.ParticipantActor.Tell(new COMBAT_106_PROTOCOL.MSG_NEWROUND());
            }
        });
    }

    private bool IsPvPMoveAllowed(CombatDuelSubCircle caster) {
        if (_pvp.ActingTeam is { } acting && caster.OccupiedTeam != acting) {
            Logger.Debug("Duel {0} | Slot {1} | Move dropped, the other team is acting.",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex));

            return false;
        }

        // The AI of a minion plans ahead; a wizard may only move once the planning phase has begun.
        if (caster.IsHumanPlayer && Duel.m_duelPhase != kDuelPhase.kPhase_Planning) {
            Logger.Debug("Duel {0} | Slot {1} | Move dropped, the duel is not in its planning phase.",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex));

            return false;
        }

        return true;
    }

    [MessageHandler(typeof(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_ENDDUEL))]
    private void DespawnDuel() {
        _isActive = false;

        // Minions are children of this sigil entity, which persists between fights; MSG_COMBATDEATH
        // deletes them outright.
        EnactActionOnSubCircles(circle => {
            if (circle.IsSummonedMinion && circle.ParticipantActor is not null) {
                circle.ParticipantActor.Tell(new COMBAT_106_PROTOCOL.MSG_COMBATDEATH());
            }
        });

        _renderComponent?.Disable();
        Entity.DespawnObject();
        _entitiesInRange.Clear();
        _groupReservations.Clear();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message) {
        if (!_isActive) {
            return;
        }

        // Find the sub circle that the client was in and remove them from the duel.
        var subCircle = SubCircles.FirstOrDefault(x => x.ParticipantActor == Sender);
        if (subCircle is null) {
            return;
        }

        _pvp?.MarkDisconnected(Sender);

        // Handle this as if it were the flee action.
        HandleFleeAction(subCircle);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message) {
        if (!_isActive) {
            return;
        }

        // Find the sub circle that the client was in and remove them from the duel.
        var subCircle = SubCircles.FirstOrDefault(x => x.ParticipantActor == Sender);
        if (subCircle is null) {
            return;
        }

        _pvp?.MarkDisconnected(Sender);

        // Handle this as if it were the flee action.
        HandleFleeAction(subCircle);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_DEFERREDMINIONSUMMON))]
    private void ReceiveDeferredMinionSummon(ZONE_102_PROTOCOL.MSG_DEFERREDMINIONSUMMON message) {
        // Drop if the duel ended or restarted during the summon animation.
        if (!_isActive || message.Caster is null || Array.IndexOf(SubCircles, message.Caster) < 0) {
            Logger.Information("Duel {0} | deferred minion summon (tid {1}) dropped, duel no longer active.",
                Logger.Args(Duel.m_duelID.Full, message.CreatureTid));

            return;
        }

        SpawnAndAssignMinion(message.CreatureTid, message.Caster);
    }

    private void InitializeDuel(Dictionary<IActorRef, CoreObject> startingParticipants) {
        Duel = CreateDuelWithDefaults();
        SubCircles = CreateDuelActorSubCircles(_sigilTemplate);
        CombatResolver = new Combat.CombatResolver(Duel, SubCircles);

        // Determine which team goes up first. Tutorial duels override this with the golems first.
        Duel.m_firstTeamToAct = (int) DetermineFirstTeam();
        _tutorialDirector.OnDuelCreated(Duel);

        // When the duel is created, it must be created by two suspects: the player and the creature.
        // The creatures will always be team A and the players will always be team B. Assign the first
        // participants to their respective sub circles.
        var startingCreatureActor = startingParticipants.First(x => x.Value.m_templateID != 1);
        var startingPlayerActor = startingParticipants.First(x => x.Value.m_templateID == 1);
        var startingCreatureObject = startingCreatureActor.Value;
        var startingPlayerObject = startingPlayerActor.Value;

        var availableCreatureSubCircle = GetAvailableSubCircleTeamCreature();
        var availablePlayerSubCircle = ChoosePlayerSubCircle(startingPlayerObject);

        if (availableCreatureSubCircle == null || availablePlayerSubCircle == null) {
            Logger.Error("Failed to find available sub circles for duel {0}", Logger.Args(SigilId));

            return;
        }

        var creatureAssigned = AssignParticipantToSubCircle(availableCreatureSubCircle, startingCreatureActor.Key, startingCreatureObject);
        var playerAssigned = AssignParticipantToSubCircle(availablePlayerSubCircle, startingPlayerActor.Key, startingPlayerObject);
        if (!creatureAssigned || !playerAssigned) {
            Logger.Error("Duel {0} | A starting participant did not answer; the duel was not started.", Logger.Args(SigilId));

            return;
        }

        _isActive = true;
        ReserveCirclesForGroupOf(startingPlayerObject);

        Logger.Debug("Duel {0} | Created. Grace period over in {1}",
            Logger.Args(Duel.m_duelID.Full, DUEL_GRACE_PERIOD_IN_SECONDS));
    }

    private Duel CreateDuelWithDefaults() => new() {
        m_duelID = SigilId,
        m_planningTimer = PLANNING_TIME,

        m_scalarDamage = _sigilTemplate.m_scalarDamagePvE,
        m_scalarResist = _sigilTemplate.m_scalarResistPvE,
        m_scalarPierce = _sigilTemplate.m_scalarPiercePvE,
        m_damageLimit = _sigilTemplate.m_damageLimitPvE,
        m_dK0 = _sigilTemplate.m_dK0PvE,
        m_dN0 = _sigilTemplate.m_dN0PvE,
        m_resistLimit = _sigilTemplate.m_resistLimitPvE,
        m_rK0 = _sigilTemplate.m_rK0PvE,
        m_rN0 = _sigilTemplate.m_rN0PvE,
        m_flatParticipantList = [],
        m_duelModifier = new DuelModifier() {
            m_battlefieldEffects = [],
            m_combatTriggers = [],
            m_gameEffects = [],
        }
    };

    private Duel CreateDuelWithPvPDefaults(PvPDuelRules rules, uint leagueId) => new() {
        m_duelID = SigilId,
        m_bPVP = true,
        m_planningTimer = PvPSettings.PracticePlanningSeconds,

        m_scalarDamage = _sigilTemplate.m_scalarDamagePvP,
        m_scalarResist = _sigilTemplate.m_scalarResistPvP,
        m_scalarPierce = _sigilTemplate.m_scalarPiercePvP,
        m_damageLimit = _sigilTemplate.m_damageLimitPvP,
        m_dK0 = _sigilTemplate.m_dK0PvP,
        m_dN0 = _sigilTemplate.m_dN0PvP,
        m_resistLimit = _sigilTemplate.m_resistLimitPvP,
        m_rK0 = _sigilTemplate.m_rK0PvP,
        m_rN0 = _sigilTemplate.m_rN0PvP,
        m_shadowThresholdFactor = _sigilTemplate.m_shadowThresholdFactor,
        m_shadowPipRatingFactor = _sigilTemplate.m_shadowPipRatingFactor,
        m_alternateTurnCombatRule = PvPGameTypes.ToCombatRule(rules),
        m_combatRules = [PvPGameTypes.ToCombatRule(rules)],
        m_flatParticipantList = [],
        m_duelModifier = new DuelModifier() {
            m_battlefieldEffects = [],
            m_combatTriggers = [],
            m_gameEffects = PvPLeagueScaling.CreateClientEffects(leagueId),
        }
    };

    private void InitializePvPDuel(ZONE_102_PROTOCOL.MSG_STARTPVPMATCH message, PvPDuelRules rules) {
        Duel = CreateDuelWithPvPDefaults(rules, message.LeagueId);
        SubCircles = CreateDuelActorSubCircles(_sigilTemplate);
        CombatResolver = new Combat.CombatResolver(Duel, SubCircles);

        var firstTeam = DetermineFirstTeam();
        Duel.m_firstTeamToAct = (int) firstTeam;
        Duel.m_originalFirstTeamToAct = (int) firstTeam;
        _pvp = new PvPDuelState(message.MatchId, message.TournamentNameId, message.LeagueId, message.Roster, rules, firstTeam);

        // The roster is fixed: each player sits on a circle of their side and is moved onto it first, because
        // they stand wherever they arrived in the zone.
        foreach (var entry in message.Roster) {
            var subCircle = GetAvailableSubCircle((CombatTeam) entry.TeamIndex)
                ?? throw new InvalidOperationException($"No free circle on side {entry.TeamIndex}.");

            ZoneBroadcast(new GAME_5_PROTOCOL.MSG_SERVERTELEPORT {
                LocationX = CompressTeleportCoordinate(subCircle.WorldPosition.X),
                LocationY = CompressTeleportCoordinate(subCircle.WorldPosition.Y),
                LocationZ = CompressTeleportCoordinate(subCircle.WorldPosition.Z),
                Direction = (byte) Math.Round(subCircle.WorldRotation / (2 * Math.PI) * 250),
                MobileID = entry.PlayerObject.m_nMobileID,
            });

            AssignParticipantToSubCircle(subCircle, entry.Session, entry.PlayerObject);
        }

        _isActive = true;

        Logger.Debug("Duel {0} | PvP match {1} created with {2} players. Grace period over in {3}",
            Logger.Args(Duel.m_duelID.Full, message.MatchId, message.Roster.Count, DUEL_GRACE_PERIOD_IN_SECONDS));
    }

    private void CancelPvPMatch(ulong matchId, PvPCancelReason reason)
        => PvPDirector.Instance?.Tell(new PVP_111_PROTOCOL.MSG_MATCHCANCELLED { MatchId = matchId, Reason = reason });

    private void AbortPvPDuel(ulong matchId, PvPCancelReason reason) {
        // Seating failed halfway: let the seated players go, then ask the director to send everyone back.
        _isActive = false;
        if (_pvp is not null) {
            foreach (var entry in _pvp.Roster) {
                entry.Session.Tell(new COMBAT_106_PROTOCOL.MSG_PVPDUELENDED { MatchId = matchId, WinningTeam = -1 });
            }

            _pvp = null;
        }

        CancelPvPMatch(matchId, reason);
    }

    private CombatDuelSubCircle[] CreateDuelActorSubCircles(CombatSigilTemplate template) {
        var subCircles = template.m_subCircles;
        var subCircleObjs = new CombatDuelSubCircle[8];
        var sigilOrientation = Entity.ActiveGameObject.m_orientation;
        var sigilLocation = Entity.ActiveGameObject.m_location;

        // The sigil rotation is stored between -pi and pi. We need it to be between 0 and 2pi.
        var sigilRotation = sigilOrientation.Z;
        if (sigilRotation < 0) {
            sigilRotation = (2 * MathF.PI) + sigilRotation;
        }

        for (int i = 0; i < subCircles.Count; i++) {
            var rotation = subCircles[i].m_rotation;
            var radius = subCircles[i].m_radius;
            var color = subCircles[i].m_color;
            var rotationRadians = rotation * (MathF.PI / 180f);

            // The sigil is rotated by some degree. We need to find our x and y coordinates based on this rotation.
            var rotatedX = radius * MathF.Cos(rotationRadians - sigilRotation);
            var rotatedY = radius * MathF.Sin(rotationRadians - sigilRotation);
            var x = sigilLocation.X + rotatedX;
            var y = sigilLocation.Y + rotatedY;
            var rotatedSigilPos = new Vector3(x, y, sigilLocation.Z);

            // Now we know where the sigil is located, we need to calculate the facing direction of the sub circle.
            // Calculate the direction vector towards the center of the duel (only Z-axis in radians)
            var duelCenter = new Vector3(sigilLocation.X - x, sigilLocation.Y - y, 0);
            var faceTowardsYaw = MathF.Atan2(duelCenter.Y, duelCenter.X);
            // The yaw must be between 0 and 2PI. It must also be reversed as the client rotates clockwise.
            // The translation isn't perfect because of Gamebyro engine bullshit. We need to compensate for this.
            faceTowardsYaw = (2 * MathF.PI) - faceTowardsYaw - YAW_ERROR_COMPENSATION;
            if (faceTowardsYaw < 0) {
                faceTowardsYaw += 2 * MathF.PI;
            }

            // Cretae the sub circle object and add it to the array.
            var subCircle = new CombatDuelSubCircle(this, radius, rotation, color, i) {
                WorldPosition = rotatedSigilPos,
                WorldRotation = faceTowardsYaw,
                SlotName = subCircles[i].m_locationPreference,
                SlotType = IsCreatureSide(subCircles[i].m_locationType) ? CombatSlotType.Creature : CombatSlotType.Player
            };
            subCircleObjs[i] = subCircle;
        }

        return subCircleObjs;
    }

    private static bool IsCreatureSide(string locationType) {
        // The red circles of a PvP sigil are the first four, like the monster circles of a combat sigil.
        return locationType is "MonsterCircle" or "RedCircle";
    }

    private void EnactActionOnSubCircles(Action<CombatDuelSubCircle> action) {
        foreach (var subCircle in ActiveSubCircles) {
            action(subCircle);
        }
    }

    private CombatDuelSubCircle GetAvailableSubCircleTeamCreature() {
        for (int i = 0; i < 4; i++) {
            if (!SubCircles[i].Occupied) {
                return SubCircles[i];
            }
        }

        return null;
    }

    private CombatDuelSubCircle GetAvailableSubCircle(CombatTeam team)
        => team == CombatTeam.Monster ? GetAvailableSubCircleTeamCreature() : GetAvailableSubCircleTeamPlayer();

    private CombatDuelSubCircle GetAvailableSubCircleTeamPlayer() {
        for (int i = 4; i < 8; i++) {
            if (!SubCircles[i].Occupied) {
                return SubCircles[i];
            }
        }

        return null;
    }

    private bool AssignParticipantToSubCircle(CombatDuelSubCircle subCircle, IActorRef actorRef, CoreObject coreObject,
                                              bool isSummonedMinion = false, int minionOwnerSubCircle = 0) {
        if (subCircle.ParticipantActor != null) {
            return false;
        }

        return subCircle.AssignParticipant(actorRef, coreObject, isSummonedMinion, minionOwnerSubCircle) is not null;
    }

    internal void SummonMinion(uint creatureTid, CombatDuelSubCircle caster) {
        if (CoreObjectFactory.GetCoreTemplate(creatureTid) is null) {
            Logger.Warning("Duel {0} | minion summon: no template for creature tid {1}.",
                Logger.Args(Duel.m_duelID.Full, creatureTid));

            return;
        }

        // Cinematics before this cast plus the summon animation: the minion appears mid-cast.
        var castOffset = CurrentActionCinematicOffsetSeconds;
        var spawnDelay = castOffset + MINION_SUMMON_ANIMATION_DELAY;
        Timers.StartSingleTimer(
            $"minionSummon_{Guid.NewGuid():N}",
            new ZONE_102_PROTOCOL.MSG_DEFERREDMINIONSUMMON { CreatureTid = creatureTid, Caster = caster },
            TimeSpan.FromSeconds(spawnDelay));
    }

    internal static void OnMinionRemoved(CombatDuelSubCircle circle)
        => circle.RemoveParticipant();

    private void SpawnAndAssignMinion(uint creatureTid, CombatDuelSubCircle caster) {
        // A PvE minion fights for the wizards whoever summoned it; in PvP it fights for its summoner.
        var slot = Duel.m_bPVP ? GetAvailableSubCircle(caster.OccupiedTeam) : GetAvailableSubCircleTeamPlayer();
        if (slot is null) {
            Logger.Information("Duel {0} | minion summon (tid {1}) skipped, no free slot on the side of the caster.",
                Logger.Args(Duel.m_duelID.Full, creatureTid));

            return;
        }

        var template = CoreObjectFactory.GetCoreTemplate(creatureTid);
        if (template is null) {
            Logger.Warning("Duel {0} | minion summon: no template for creature tid {1}.",
                Logger.Args(Duel.m_duelID.Full, creatureTid));

            return;
        }

        var centre = Entity.ActiveGameObject?.m_location ?? (caster?.ParticipantObject?.m_location ?? default);
        var info = new CoreObjectInfo {
            m_templateID = creatureTid,
            m_location = centre,
            m_fScale = 1.0f,
        };
        var minionObj = CoreObjectFactory.FinalizeCoreObject(info, template);
        minionObj = CoreObjectFactory.InitializeCoreObjectBehaviors(minionObj, template);

        var minionActor = Entity.SpawnCombatMinionActor(minionObj, template);
        if (minionActor is null) {
            return;
        }

        try {
            AssignParticipantToSubCircle(slot, minionActor, minionObj, isSummonedMinion: true,
                                         minionOwnerSubCircle: caster.SlotIndex);
        }
        catch (Exception ex) {
            Logger.Error("Duel {0} | minion summon: failed to assign tid {1} to slot {2}: {3}",
                Logger.Args(Duel.m_duelID.Full, creatureTid, slot.SlotIndex, ex));
            slot.RemoveParticipant();
            minionActor.Tell(PoisonPill.Instance);

            return;
        }

        Logger.Information("Duel {0} | summoned minion tid {1} into player-team slot {2} (caught up next round).",
            Logger.Args(Duel.m_duelID.Full, creatureTid, slot.SlotIndex));
    }

    private void AddParticipant(CoreObject participantObject, IActorRef participantActor) {
        var isPlayer = participantObject.m_templateID == 1;

        var alreadyInDuel = SubCircles.Any(x => x?.ParticipantObject?.m_globalID == participantObject.m_globalID);
        if (alreadyInDuel) {
            return;
        }

        var subCircle = isPlayer ? ChoosePlayerSubCircle(participantObject) : GetAvailableSubCircleTeamCreature();
        if (subCircle is null) {
            if (isPlayer && _groupReservations.Count > 0) {
                Logger.Debug("Duel {DuelId} | Every free circle is held for a group; {Participant} cannot join.",
                    Logger.Args(Duel.m_duelID.Full, participantObject.m_debugName));

                return;
            }

            Logger.Warning("Duel {0} | No available sub circles for participant {1}",
                Logger.Args(Duel.m_duelID.Full, participantObject.m_globalID));

            return;
        }

        AssignParticipantToSubCircle(subCircle, participantActor, participantObject);

        Logger.Debug("Duel {0} | Slot {1} | Participant {2} joined",
            Logger.Args(Duel.m_duelID.Full, subCircle.SlotIndex, participantObject.m_debugName));

        if (isPlayer) {
            ReserveCirclesForGroupOf(participantObject);
        }
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_JOINRESERVEDDUEL))]
    private void ReceiveJoinReservedDuel(GROUP_109_PROTOCOL.MSG_JOINRESERVEDDUEL message) {
        if (!_isActive || !_groupReservations.IsReservedFor(message.CharId)) {
            return;
        }

        var subCircle = _groupReservations.ChoosePlayerCircle(message.CharId, SubCircles);
        if (subCircle is null) {
            return;
        }

        // The group mate may be anywhere in the zone; put them on their circle before they join.
        ZoneBroadcast(new GAME_5_PROTOCOL.MSG_SERVERTELEPORT {
            LocationX = CompressTeleportCoordinate(subCircle.WorldPosition.X),
            LocationY = CompressTeleportCoordinate(subCircle.WorldPosition.Y),
            LocationZ = CompressTeleportCoordinate(subCircle.WorldPosition.Z),
            Direction = (byte) Math.Round(subCircle.WorldRotation / (2 * Math.PI) * 250),
            MobileID = message.PlayerObject.m_nMobileID,
        });

        AddParticipant(message.PlayerObject, message.PlayerActor);
    }

    private CombatDuelSubCircle ChoosePlayerSubCircle(CoreObject playerObject) {
        if (!Wizard.TryGetCharacterId(playerObject.m_globalID, out var charId)) {
            return GetAvailableSubCircleTeamPlayer();
        }

        var subCircle = _groupReservations.ChoosePlayerCircle(charId, SubCircles);
        if (subCircle is not null) {
            _groupReservations.Release(charId);
        }

        return subCircle;
    }

    private void ReserveCirclesForGroupOf(CoreObject playerObject) {
        if (IsScriptedDuel() || IsPvPDuel || !Wizard.TryGetCharacterId(playerObject.m_globalID, out var charId)) {
            return;
        }

        foreach (var groupMate in _groupReservations.ReserveForGroupOf(charId, ZoneActor, SubCircles)) {
            Logger.Debug("Duel {DuelId} | Holding a circle for group mate {GroupMate} of {CharId}.",
                Logger.Args(Duel.m_duelID.Full, groupMate.CharId, charId));

            // Accepting sends MSG_GotoPlayerConfirm back, which GroupService turns into MSG_JOINRESERVEDDUEL.
            groupMate.Session?.Tell(new WIZARD2_53_PROTOCOL.MSG_GotoPlayerConfirm {
                PromptKey = GROUP_JOIN_PROMPT_KEY,
                TargetCharacterID = charId,
                DisableCrossPlay = 0,
                ClientPlatform = 0
            });
        }
    }

    private void ApplyFullGroupInitiative() {
        if (IsScriptedDuel() || IsPvPDuel || !CombatGroupReservations.IsFullGroup(SubCircles)) {
            return;
        }

        Duel.m_fullPartyGroup = true;
        Duel.m_firstTeamToAct = (int) CombatTeam.Player;

        // The client reads the full group flag from the duel object when its combat intro starts.
        BroadcastDuel();

        Logger.Debug("Duel {DuelId} | A full group takes the first turn.", Logger.Args(Duel.m_duelID.Full));
    }

    private void BroadcastDuel() {
        var duelBehavior = GetClientBehaviorInstance();
        if (_serializer.Serialize(duelBehavior, _combatParticipantFlags, out var duelData)) {
            ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_DUEL {
                Data = duelData,
            });
        }
    }

    private static ushort CompressTeleportCoordinate(float coordinate)
        => unchecked((ushort) (short) MathF.Round(coordinate / 4));

    private void SendCombatPhase(byte phase) {
        // Determine which sigil slot the client should point its turn indicator at.
        var upFirstSigilSlot = GetUpFirstSigilSlot();

        // Serialize the up first data and send it to all the combat participants.
        // This is the one instance where the client sends a versionable object to the client.
        var versionableSerializer = new ObjectSerializer(
            Versionable: true,
            Behaviors: SerializerFlags.None
        );

        var upFirst = new UpFirstData {
            m_resultType = 122, // Always recorded as 122, per packet captures.
            m_upFirst = upFirstSigilSlot,
            m_roundNum = Duel.m_roundNum,
        };
        if (!versionableSerializer.Serialize(upFirst, _upFirstFlags, out var upFirstData)) {
            Logger.Error("Failed to serialize up first data for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATPHASE() {
            DuelID = SigilId,
            NewPhase = phase,
            PlayerID = 0, // Always recorded as 0
            Data = phase == 1 ? upFirstData : "",
        };

        PlayerBroadcast(msg);
    }

    private void SendUpFirst(int roundNum) {
        var upFirstSigilSlot = GetUpFirstSigilSlot();

        var upFirstMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATUPFIRST {
            DuelID = SigilId,
            RoundNum = (ushort) roundNum,
            FirstTeamToAct = (byte) Duel.m_firstTeamToAct,
            UpFirst = upFirstSigilSlot,
        };
        ZoneBroadcast(upFirstMsg);
    }

    private void SendCombatUI(byte planningPhaseTimer) {
        // When the teams take turns, every client is told whose turn it is and only that team gets the countdown.
        var acting = IsPvPDuel ? _pvp.ActingTeam : null;
        var combatUiMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_SHOWCOMBATUI {
            DuelID = SigilId,
            AltTurn = acting is null ? 0 : 1,
            AltTurnTeam = (int?) acting ?? 0,
        };
        var planningMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_SETPLANNINGPHASETIMER {
            DuelID = SigilId,
            Time = planningPhaseTimer,
        };

        HumanBroadcast(combatUiMsg);
        if (acting is { } actingTeam) {
            TeamBroadcast(actingTeam, planningMsg);
        } else {
            HumanBroadcast(planningMsg);
        }
    }

    private void SendCombatStats() => EnactActionOnSubCircles(circle => {
        // Serialize participant stats and send them to the participant, locally.
        var participantCombatsStats = circle.ClientGameStats;
        if (participantCombatsStats is null) {
            Logger.Error("Failed to get combat stats for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        if (!_serializer.Serialize(participantCombatsStats, _combatParticipantStatFlags, out var buffer)) {
            Logger.Error("Failed to serialize combat stats for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATSTATS {
            DuelID = SigilId,
            PartID = circle.ParticipantObject.m_globalID,
            StatsData = buffer,
        };

        PlayerBroadcast(msg);
    });

    private void SendCombatHand() {
        // Serialize the combat hand and send it to the participant, locally.
        // We're skipping creatures for now.
        EnactActionOnSubCircles(circle => {
            if (circle.IsAiCreature) {
                return;
            }

            var newHand = circle.DrawHand();

            if (!_serializer.Serialize(newHand, _combatParticipantHandFlags, out var buffer)) {
                Logger.Error("Failed to serialize combat hand for duel {0}",
                    Logger.Args(SigilId));

                return;
            }

            var participantActor = circle.ParticipantActor;
            var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATHAND {
                DeckCount = (byte) circle.AvailableSpells,
                TotalDeckCount = (ushort) circle.TotalSpells,
                TreasureCardCount = (ushort) circle.VaultRemainingCount,
                ParticipantID = circle.ParticipantObject.m_globalID,
                HandData = buffer,
            };

            participantActor.Tell(msg);
        });
    }

    internal void SendCurrentCombatHand(CombatDuelSubCircle circle) {
        // As-is, no draw or refill, so a discarded slot stays visibly open for the vault draw.
        if (circle is null || circle.IsAiCreature) {
            return;
        }

        var hand = circle.GetCurrentHand();
        if (!_serializer.Serialize(hand, _combatParticipantHandFlags, out var buffer)) {
            Logger.Error("Failed to serialize current combat hand for duel {0}", Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATHAND {
            DeckCount = (byte) circle.AvailableSpells,
            TotalDeckCount = (ushort) circle.TotalSpells,
            TreasureCardCount = (ushort) circle.VaultRemainingCount,
            ParticipantID = circle.ParticipantObject.m_globalID,
            HandData = buffer,
        };

        circle.ParticipantActor.Tell(msg);
    }

    internal void SendCombatPips() {
        var pips = new CombatPipListObj {
            m_pipList = new List<ParticipantPipData>(),
            m_duelID = SigilId
        };

        EnactActionOnSubCircles(circle => {
            if (!circle.AddedToDuel || !circle.IsAlive) {
                return;
            }

            var participantPipData = new ParticipantPipData {
                m_acq = circle.AcquiredPipType,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
                m_pips = CombatPipPayment.Copy(circle.CombatParticipant.m_pipCount),
                m_arch = circle.CombatParticipant.m_archmasterySchool,
                m_archPoints = circle.CombatParticipant.m_archmasteryPoints,
            };
            pips.m_pipList.Add(participantPipData);
        });

        // Serialize the combat pips and send it to each participant.
        if (!_serializer.Serialize(pips, _combatParticipantStatFlags, out var buffer)) {
            Logger.Error("Failed to serialize combat pips for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATPIPS {
            DuelID = SigilId,
            PipData = buffer,
        });
    }

    private void SendCombatHealth() {
        var healthList = new CombatHealthListObj {
            m_healthList = new List<ParticipantParameter>(),
            m_duelID = SigilId
        };

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

        // Serialize the combat health and send it to each participant.
        if (!_serializer.Serialize(healthList, _combatParticipantStatFlags, out var healthBuffer)) {
            Logger.Error("Failed to serialize combat health for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATHEALTH {
            DuelID = SigilId,
            HealthData = healthBuffer,
        };
        ZoneBroadcast(msg);
    }

    private void SendCombatMoveSelection(CombatTeam team, ulong participantId, byte moveType, Spell spell, byte targetIndex) {
        byte isItemCard = (byte) (spell?.m_itemCard ?? false ? 1 : 0);
        byte isTreasureCard = (byte) (spell?.m_treasureCard ?? false ? 1 : 0);
        byte isBattleCard = (byte) (spell?.m_battleCard ?? false ? 1 : 0);

        var actualIndex = (byte) Math.Pow(2, targetIndex);

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATMOVESELECTION {
            DuelID = SigilId,
            ParticipantID = participantId,
            MoveType = moveType,
            SpellID = (int) (spell?.m_templateID ?? 0),
            SpellTargetIndex = actualIndex,
            IsItemCard = isItemCard,
            IsTreasureCard = isTreasureCard,
            IsBattleCard = isBattleCard,
        };
        // The chosen card is for the side of the caster only; the other side sees it when it is cast.
        TeamBroadcast(team, msg);
    }

    private void HandleDiscardMove(CombatDuelSubCircle caster, int spellSelection) {
        var spell = caster.GetSpellFromLastHand((byte) spellSelection);
        if (spell is null) {
            Logger.Warning("Duel {0} | Slot {1} | Discard received for an empty hand slot ({2}).",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spellSelection));

            return;
        }

        caster.DiscardCard(spell);

        Logger.Debug("Duel {0} | Slot {1} | Discarded a card: {2}",
            Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spell.m_templateID.ToString() ?? "None"));

        // Re-send as-is so the freed slot stays open; SendCombatHand would refill it from the deck.
        SendCurrentCombatHand(caster);
    }

    private void HandlePassMove(CombatDuelSubCircle caster) {
        // If the participant passes, we don't need to know what spell they were casting.
        CombatResolver.AddCombatMove(CombatMoveType.Pass, caster, null, null);

        if (!caster.IsAiCreature) {
            SendCombatMoveSelection(caster.OccupiedTeam, caster.ParticipantObject.m_globalID, (byte) CombatMoveType.Pass, null, 0);
        }
    }

    private void HandleAttackMove(CombatDuelSubCircle caster, int spellSelection, uint spellTarget) {
        var spell = caster.GetSpellFromLastHand((byte) spellSelection);
        if (spell is null) {
            Logger.Warning("Duel {0} | Slot {1} | Attack received for an empty hand slot ({2}).",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spellSelection));

            CombatResolver.AddCombatMove(CombatMoveType.Pass, caster, null, null);

            return;
        }

        if (!caster.HasPipsForSpell(spell)) {
            Logger.Warning("Duel {0} | Slot {1} | Participant does not have enough pips for spell {2}",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spell.m_templateID));

            CombatResolver.AddCombatMove(CombatMoveType.Pass, caster, null, null);

            return;
        }

        // Find the target sub circle. Valid targets are 1-8.
        // If the spell doesn't have a target like for AoE spells or self-heals,
        // the value will be the integer cap.
        var target = caster;
        if (spellTarget < SubCircles.Length) {
            // The client sends raw sigil slots for spell targets (see GetUpFirstSigilSlot).
            target = SubCircles[spellTarget];
        }

        CombatResolver.AddCombatMove(CombatMoveType.Attack, caster, target, spell);

        // Minions are AI-driven; their telegraph comes from ResendMinionMoveSelections, not an echo.
        if (!caster.IsSummonedMinion && !caster.IsAiCreature) {
            SendCombatMoveSelection(caster.OccupiedTeam, caster.ParticipantObject.m_globalID, (byte) CombatMoveType.Attack, spell, (byte) spellTarget);
        }
    }

    private void HandleChangeMindAction(CombatDuelSubCircle caster) {
        // Send the action director a null spell to indicate that the participant has changed their mind.
        CombatResolver.AddCombatMove(CombatMoveType.ChangeMind, caster, null, null);

        // Echo the change mind action to each player participant
        if (!caster.IsAiCreature) {
            SendCombatMoveSelection(caster.OccupiedTeam, caster.ParticipantObject.m_globalID, (byte) CombatMoveType.ChangeMind, null, 0);
        }
    }

    private void HandleFleeAction(CombatDuelSubCircle caster) {
        if (!_isActive) {
            return;
        }

        if (IsPvPDuel) {
            ForfeitPvPDuel(caster);

            return;
        }

        var actor = caster.ParticipantActor;
        var participantObjId = caster.ParticipantObject.m_globalID;

        // Fleeing drains the player's mana; creature participants have no wizard.
        if (caster._wizard is not null) {
            var clientMaxMana = caster._wizard.GameStats.GetClientTypeAlternative().m_baseMana;
            caster._wizard.UpdateMana(0);
            actor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                Mana = 0,
                MaxMana = clientMaxMana,
            });
        }

        // Inform the client that they've been removed from this duel.
        var defeatMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEFEAT();
        actor.Tell(defeatMsg);

        caster.RemoveParticipant();

        Logger.Debug("Duel {0} | Slot {1} | Participant fled",
            Logger.Args(Duel.m_duelID.Full, caster.SlotIndex));

        ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATREMOVE {
            DuelID = SigilId,
            ParticipantID = participantObjId,
        });

        // If no more players are left in the duel because of this, end the duel.
        if (AliveAndInDuelPlayerCount <= 0) {
            EndDuel();
        }
    }

    private static CombatTeam DetermineFirstTeam()
        => (CombatTeam) new Random().Next(0, 2);

    internal bool IsScriptedDuel()
        => _tutorialDirector.IsActive;

    private byte GetUpFirstSigilSlot() {
        // Prefer the acting team's first living participant, else any living participant.
        var upFirst = SubCircles.FirstOrDefault(s => s is not null && s.AddedToDuel && s.IsAlive
            && s.SlotType == (Duel.m_firstTeamToAct == (int) CombatTeam.Player
                ? CombatSlotType.Player
                : CombatSlotType.Creature))
            ?? SubCircles.FirstOrDefault(s => s is not null && s.AddedToDuel && s.IsAlive);
        if (upFirst is null) {
            return 0;
        }

        // The modern client resolves the indicator against its sigil slots, not a list index.
        return (byte) upFirst.SlotIndex;
    }

    private void AddWaitingCombatParticipants() => EnactActionOnSubCircles(circle => {
        if (circle.AddedToDuel || !circle.Occupied) {
            return;
        }

        // Serialize the combat participant and send it to every client in the zone.
        var participant = circle.CombatParticipant;
        if (!_serializer.Serialize(participant, _combatParticipantFlags, out var buffer)) {
            Logger.Error("Failed to serialize combat participant for duel {0}",
                Logger.Args(SigilId));

            return;
        }

        var msg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATADD {
            DuelID = SigilId,
            ParticipantData = buffer,
        };
        ZoneBroadcast(msg);

        Logger.Debug("Duel {0} | Slot {1} | Serialized participant sent",
            Logger.Args(Duel.m_duelID.Full, circle.SlotIndex));

        circle.AddedToDuel = true;
        Duel.m_flatParticipantList.Add(participant);
    });

    private void LogStartingPips() {
        var pips = new List<string>();
        EnactActionOnSubCircles(circle => {
            if (circle.AddedToDuel) {
                pips.Add($"{circle.SlotType}{circle.SlotIndex}={circle.CombatParticipant.m_pipCount.m_genericPips}+{circle.CombatParticipant.m_pipCount.m_powerPips}p");
            }
        });
        Logger.Debug("Duel {0} | Starting pips: {1}", Logger.Args(Duel.m_duelID.Full, string.Join(", ", pips)));
    }

    private bool IsAlternatingPvP => IsPvPDuel && _pvp.Rules.AlternateTurns;

    private void DoPipGain(CombatTeam? onlyTeam = null) {
        EnactActionOnSubCircles(circle => {
            if (!circle.AddedToDuel || !circle.IsAlive || circle.CombatParticipant.m_PipsSuspended) {
                return;
            }

            if (onlyTeam is { } team && circle.OccupiedTeam != team) {
                return;
            }

            circle.DoPipGain();
            CombatShadowPips.RollShadowPip(circle);
            CombatArchmastery.Accumulate(circle, SubCircles);
        });

        var pips = new List<string>();
        EnactActionOnSubCircles(circle => {
            if (circle.AddedToDuel && (onlyTeam is null || circle.OccupiedTeam == onlyTeam)) {
                pips.Add($"{circle.SlotType}{circle.SlotIndex}={circle.CombatParticipant.m_pipCount.m_genericPips}+{circle.CombatParticipant.m_pipCount.m_powerPips}p");
            }
        });
        Logger.Debug("Duel {0} | Pip gain for {1}: {2}",
            Logger.Args(Duel.m_duelID.Full, onlyTeam?.ToString() ?? "all teams", string.Join(", ", pips)));
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_CHEATINSTAWIN))]
    private void ReceiveCheatInstaWin(COMBAT_106_PROTOCOL.MSG_CHEATINSTAWIN message) {
        // The cheat kills the creatures; the red side of a PvP duel is made of wizards.
        if (!_isActive || IsPvPDuel) {
            return;
        }

        // EndDuel derives the winner from the alive creature count, so zero them first.
        Timers.CancelAll();
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster) {
                circle.DamageParticipant(int.MaxValue);
            }
        });

        EndDuel();
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_CHEATINSTANTCINEMATICS))]
    private void ReceiveCheatInstantCinematics(COMBAT_106_PROTOCOL.MSG_CHEATINSTANTCINEMATICS message)
        => CheatInstantCinematics = message.Enabled;

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_CHEATNOFIZZLE))]
    private void ReceiveCheatNoFizzle(COMBAT_106_PROTOCOL.MSG_CHEATNOFIZZLE message) {
        var subCircle = SubCircles.FirstOrDefault(x => x.ParticipantActor == message.Actor);
        if (subCircle is null) {
            return;
        }

        subCircle.CheatNoFizzle = message.Enabled;
    }

    private void EndDuel() {
        if (IsPvPDuel) {
            EndPvPDuel();

            return;
        }

        // The duel has ended. Inform the clients of the result.
        var playersWin = AliveAndInDuelCreatureCount <= 0;
        var creaturesWin = AliveAndInDuelPlayerCount <= 0;

        // A queued tutorial card grant must not leak into the next duel on this sigil.
        _tutorialDirector.OnDuelEnded();

        RemovePlayersFromDuel();

        if (playersWin) {
            PlayerWin();
        }
        else if (creaturesWin) {
            CreatureWin();
        }

        // Broadcast to the zone of the result.
        var combatMatchResult = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATMATCHRESULT {
            DuelID = SigilId,
            WinningTeam = playersWin ? (byte) CombatTeam.Player : (byte) CombatTeam.Monster,
        };
        ZoneBroadcast(combatMatchResult);

        // Inform the zone of the final phase, and the end of the duel.
        Duel.m_duelPhase = kDuelPhase.kPhase_Ended;
        SendCombatPhase((byte) Duel.m_duelPhase);

        var duelEndedMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_ENDDUEL { DuelID = SigilId };
        ZoneBroadcast(duelEndedMsg);

        DespawnDuel();
        _isActive = false;

        // Nobody is in this duel any more; a later disconnect or logout must not find a circle.
        EnactActionOnSubCircles(circle => circle.RemoveParticipant());
    }

    private bool IsPvPSideWiped()
        => AliveHumanCount(CombatTeam.Player) <= 0 || AliveHumanCount(CombatTeam.Monster) <= 0;

    private int GetPvPWinningTeam() {
        // The side with a wizard still standing wins; with none standing on either side it is a draw (-1).
        var blueStands = AliveHumanCount(CombatTeam.Player) > 0;
        var redStands = AliveHumanCount(CombatTeam.Monster) > 0;

        return blueStands == redStands ? -1 : blueStands ? (int) CombatTeam.Player : (int) CombatTeam.Monster;
    }

    private void ForfeitPvPDuel(CombatDuelSubCircle caster) {
        var participantObjId = caster.ParticipantObject.m_globalID;
        var session = caster.ParticipantActor;
        var matchWasDecided = _pvp.IsEnding;
        if (caster._wizard is not null && !matchWasDecided) {
            _pvp.MarkForfeited(caster._wizard.CharId);
        }

        // A forfeit costs the wizard nothing outside of the duel: no mana is drained and nobody is sent to a hub.
        caster.RemoveParticipant();

        Logger.Debug("Duel {0} | Slot {1} | Participant left{2}",
            Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, matchWasDecided ? " after the match was decided" : " and forfeits"));

        ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATREMOVE {
            DuelID = SigilId,
            ParticipantID = participantObjId,
        });

        if (matchWasDecided) {
            // Somebody who leaves during the victory scene is not waited for.
            if (_pvp.StopWaitingFor(session)) {
                FinishPvPDuel();
            }

            return;
        }

        if (IsPvPSideWiped()) {
            EndDuel();
        }
    }

    private void EndPvPDuel() {
        var winningTeam = GetPvPWinningTeam();

        // Two forfeits, or a forfeit and a knockout, can both reach for the end; the match ends once.
        var waitingFor = SubCircles.Where(circle => circle.Occupied && circle.IsHumanPlayer && !_pvp.IsDisconnected(circle.ParticipantActor))
            .Select(circle => circle.ParticipantActor)
            .ToList();
        if (!_pvp.BeginEnding(winningTeam, waitingFor)) {
            return;
        }

        Logger.Debug("Duel {0} | PvP match ended. Winning team {1}.", Logger.Args(Duel.m_duelID.Full, winningTeam));

        Timers.CancelAll();

        // The wizards can move again. Nobody gains experience or loot, or is sent to a hub.
        EnactActionOnSubCircles(circle => {
            if (circle.IsHumanPlayer) {
                circle.ParticipantActor.Tell(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                    GameObjectID = circle.ParticipantObject.m_globalID,
                    State = (uint) NPCStates.Idle
                });
            }
        });

        ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATMATCHRESULT {
            DuelID = SigilId,
            WinningTeam = winningTeam,
        });
        HumanBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_ALLOWLEAVEPVP());

        Duel.m_duelPhase = kDuelPhase.kPhase_Victory;
        SendCombatPhase((byte) Duel.m_duelPhase);

        // Each client reports when its victory scene is over; a draw plays none and reports nothing, so the wait is
        // bounded by the grace period either way.
        var grace = TimeSpan.FromSeconds(PVP_RETURN_GRACE_SECONDS);
        if (grace > TimeSpan.Zero && _pvp.HasSessionsToWaitFor) {
            Timers.StartSingleTimer(PVP_RETURN_GRACE_KEY, new COMBAT_106_PROTOCOL.MSG_PVPENDGRACEOVER(), grace);

            return;
        }

        FinishPvPDuel();
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_PVPVICTORYSEEN))]
    private void ReceivePvPVictorySeen(COMBAT_106_PROTOCOL.MSG_PVPVICTORYSEEN message) {
        if (IsPvPDuel && _isActive && _pvp.IsEnding && _pvp.StopWaitingFor(message.Actor)) {
            FinishPvPDuel();
        }
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_PVPENDGRACEOVER))]
    private void ReceivePvPEndGraceOver(COMBAT_106_PROTOCOL.MSG_PVPENDGRACEOVER message) {
        if (IsPvPDuel && _isActive) {
            FinishPvPDuel();
        }
    }

    private void FinishPvPDuel() {
        if (!_pvp.TryFinish()) {
            return;
        }

        Timers.Cancel(PVP_RETURN_GRACE_KEY);

        Duel.m_duelPhase = kDuelPhase.kPhase_Ended;
        SendCombatPhase((byte) Duel.m_duelPhase);

        ZoneBroadcast(new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_ENDDUEL { DuelID = SigilId });

        ReportPvPDuelEnded(_pvp.WinningTeam);

        DespawnDuel();
        _isActive = false;
        EnactActionOnSubCircles(circle => circle.RemoveParticipant());
    }

    private void ReportPvPDuelEnded(int winningTeam) {
        // Each player's session lets go of the duel; the director sends everyone back and closes the match.
        foreach (var entry in _pvp.Roster) {
            if (_pvp.IsDisconnected(entry.Session)) {
                continue;
            }

            entry.Session.Tell(new COMBAT_106_PROTOCOL.MSG_PVPDUELENDED {
                MatchId = _pvp.MatchId,
                WinningTeam = winningTeam,
                Forfeited = _pvp.HasForfeited(entry.CharId)
            });
        }

        PvPDirector.Instance?.Tell(new PVP_111_PROTOCOL.MSG_MATCHENDED {
            MatchId = _pvp.MatchId,
            WinningTeam = winningTeam,
            ForfeitedCharIds = _pvp.ForfeitedCharIds
        });
    }

    private void PlayerWin() {
        Logger.Debug("Duel {0} | Duel ended. Players win.", Logger.Args(Duel.m_duelID.Full));

        Duel.m_duelPhase = kDuelPhase.kPhase_Victory;
        SendCombatPhase((byte) Duel.m_duelPhase);

        var adjectivesOfDefeatedMobs = new List<string>();
        var adjectivesPerDefeatedMob = new List<List<string>>();
        var templateIdsOfDefeatedMobs = new List<ulong>();
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster) {
                var mobTemplateId = circle.ParticipantObject.m_templateID;
                var mobTemplate = CoreObjectFactory.GetCoreTemplate(mobTemplateId);
                if (mobTemplate is null) {
                    return;
                }

                if (mobTemplate is not GameObjectTemplate gameObjectTemplate) {
                    return;
                }

                var mobAdjectives = gameObjectTemplate.m_adjectiveList;
                adjectivesOfDefeatedMobs.AddRange(mobAdjectives.Select(a => (string)a));
                adjectivesPerDefeatedMob.Add([.. mobAdjectives]);
                templateIdsOfDefeatedMobs.Add(gameObjectTemplate.m_templateID);
            }
        });

        var allyCharIds = new List<ulong>();
        EnactActionOnSubCircles(circle => {
            if (CombatGroupReservations.TryGetCharId(circle, out var charId)) {
                allyCharIds.Add(charId);
            }
        });

        // Send the final messages to the participants.
        var combatVictoryMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATVICTORY();
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster) {
                return;
            }

            circle.ParticipantActor.Tell(combatVictoryMsg);

            var victoryMsg = new COMBAT_106_PROTOCOL.MSG_COMBATWIN() {
                UsedPips = circle._usedPipsForExperienceGain,
                MobAdjectives = [.. adjectivesOfDefeatedMobs],
                MobTemplateIds = [.. templateIdsOfDefeatedMobs],
                AllyCharIds = [.. allyCharIds],
            };
            circle.ParticipantActor.Tell(victoryMsg);
        });

        PostMonsterKilledEvents(adjectivesPerDefeatedMob);
    }

    private void PostMonsterKilledEvents(List<List<string>> adjectivesPerDefeatedMob) {
        // Triggers match the killed monster's adjectives (a boss's ".AdjRef") and act for the first winning player.
        IActorRef winnerActor = null;
        CoreObject winnerObject = null;
        EnactActionOnSubCircles(circle => {
            if (winnerActor is null && circle.OccupiedTeam == CombatTeam.Player && !circle.IsSummonedMinion
                && circle.ParticipantActor is not null) {
                winnerActor = circle.ParticipantActor;
                winnerObject = circle.ParticipantObject;
            }
        });

        if (winnerActor is null) {
            return;
        }

        foreach (var adjectives in adjectivesPerDefeatedMob) {
            Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = MONSTER_KILLED_EVENT,
                PlayerActor = winnerActor,
                PlayerGameObject = winnerObject,
                Adjectives = adjectives,
            });
        }
    }

    private void CreatureWin() {
        Logger.Debug("Duel {0} | Duel ended. Creatures win.", Logger.Args(Duel.m_duelID.Full));

        // Send combat death to all creatures anyways. This will get rid of their game object.
        var deathMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEATH();
        EnactActionOnSubCircles(circle => circle.ParticipantActor.Tell(deathMsg));

        // Inform each player that they've been defeated.
        var defeatMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEFEAT();
        EnactActionOnSubCircles(circle => {
            if (!circle.AddedToDuel) {
                return;
            }

            circle.ParticipantActor.Tell(defeatMsg);
        });
    }

    private void RemovePlayersFromDuel() => EnactActionOnSubCircles(circle => {
        // Minions have no hub to return to; DespawnDuel tears them down.
        if (!circle.IsHumanPlayer) {
            return;
        }

        // Send any dead players back to the hub.
        if (!circle.IsAlive) {
            var defeatMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEFEAT();
            circle.ParticipantActor.Tell(defeatMsg);

            return;
        }

        // Get the players back into the idle state, so they can move around again.
        var stateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = circle.ParticipantObject.m_globalID,
            State = (uint) NPCStates.Idle
        };
        circle.ParticipantActor.Tell(stateMsg);
    });

    private bool IsParticipant(IActorRef actor)
        => SubCircles.Any(x => x is not null && x.ParticipantActor == actor);

    private bool IsSlotAvailable(CombatTeam team) {
        if (team == CombatTeam.Player) {
            return PlayerCount < TEAM_SLOT_COUNT;
        }

        // Every player past the first lets one more enemy join.
        var maxCreatures = GetBaseEnemyCount() + PlayerCount - 1;

        return CreatureCount < TEAM_SLOT_COUNT && CreatureCount < maxCreatures;
    }

    private int GetBaseEnemyCount() {
        var baseEnemyCount = _combatSigilObjectInfo?.m_baseEnemyCount ?? 0;
        if (baseEnemyCount == ONE_ENEMY_PER_PLAYER_MARKER) {
            return 1;
        }

        return baseEnemyCount > 0 ? baseEnemyCount : DEFAULT_BASE_ENEMY_COUNT;
    }

    private void ResendMinionMoveSelections() {
        EnactActionOnSubCircles(circle => {
            if (!circle.IsSummonedMinion || !circle.IsAlive || circle.ParticipantObject is null) {
                return;
            }

            if (IsPvPDuel && _pvp.ActingTeam is { } acting && circle.OccupiedTeam != acting) {
                return;
            }

            var action = CombatResolver.GetQueuedAction(circle);
            if (action is null || action.Spell is null || action.SelectedTarget is null) {
                SendCombatMoveSelection(circle.OccupiedTeam, circle.ParticipantObject.m_globalID, (byte) CombatMoveType.Pass, null, 0);

                return;
            }

            SendCombatMoveSelection(circle.OccupiedTeam, circle.ParticipantObject.m_globalID, (byte) CombatMoveType.Attack,
                action.Spell, (byte) action.SelectedTarget.SlotIndex);
        });
        var delay = TimeSpan.FromSeconds(PlanningSeconds);
        Timers.StartSingleTimer(PLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER(), delay);
    }

}