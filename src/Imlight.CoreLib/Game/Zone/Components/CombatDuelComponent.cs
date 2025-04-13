/*
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
 * Combat positions are determined by sigil templates, with specific subcircle positions.
 * 
 * TODO:
 * - Implementation of creature stunning functionality
 * - Calculate the correct damage percent max as a limit function
 * - `IsNewbieZone` and `IsDangerousZone` should be elements of the zone itself
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
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
    private const float DUEL_NEW_ROUND_DELAY = 2.5f;
    private const float YAW_ERROR_COMPENSATION = 1.58f;
    private const string GRACE_TIME_KEY = "GracePeriod";
    private const string PLANNING_TIME_KEY = "PlanningPhase";
    private const string PREPLANNING_TIME_KEY = "PrePlanningPhase";
    private const string RESOUTION_TIME_KEY = "ResolutionPhase";

    public bool NoTransfer { get; set; } = false;
    public ITimerScheduler Timers { get; set; }
    public Duel Duel { get; private set; }
    public Combat.CombatResolver CombatResolver { get; private set; }
    public CombatDuelSubCircle[] SubCircles { get; private set; }
    public CombatDuelSubCircle[] ActiveSubCircles => [.. SubCircles.Where(x => x.Occupied)];
    public byte PlayerCount => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player);
    public byte CreatureCount => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster);
    public byte AlivePlayerCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.IsAlive);
    public byte AliveCreatureCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.IsAlive);
    public byte PlayersInDuel
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.AddedToDuel);
    public byte CreaturesInDuel
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.AddedToDuel);
    public byte AliveAndInDuelPlayerCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Player && x.IsAlive && x.AddedToDuel);
    public byte AliveAndInDuelCreatureCount
        => (byte) SubCircles.Count(x => x.Occupied && x.OccupiedTeam == CombatTeam.Monster && x.IsAlive && x.AddedToDuel);
    public ulong SigilId => Entity.ActiveGameObject.m_globalID;

    private readonly Dictionary<CoreObject, IActorRef> _entitiesInRange = [];
    private readonly ObjectSerializer _serializer = new(
        Versionable: false,
        Behaviors: SerializerFlags.None
    );
    private readonly PropertyFlags _combatParticipantFlags     = (PropertyFlags) 4;
    private readonly PropertyFlags _combatParticipantStatFlags = (PropertyFlags) 5;
    private readonly PropertyFlags _combatParticipantHandFlags = (PropertyFlags) 5;
    private readonly PropertyFlags _upFirstFlags = PropertyFlags.Prop_Transmit 
                                                 | PropertyFlags.Prop_AuthorityTransmit 
                                                 | PropertyFlags.Prop_Public;

    private CombatSigilObjectInfo _combatSigilObjectInfo;
    private RenderComponent _renderComponent;
    private CombatSigilTemplate _sigilTemplate;
    private bool _isActive;
    private bool _awaitingCombatMoves;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is not null && x.m_behaviorName == "DuelBehavior");

    public override void OnStart() {
        // Disable the RenderComponent. We'll activate it when the sigil is activated.
        _renderComponent = Entity.GetComponentOfType<RenderComponent>();
        _renderComponent?.Disable();
    }

    public WizardClientDuelBehavior GetClientBehaviorInstance() => new() {
        m_pDuel = Duel,
        // todo: this is the sigil template itself, not for the game object
        m_sigilTemplateID = 1901671683,
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
        if (!_isActive) {
            return;
        }

        // Check if the player is now in range of the object.
        // If there's a slot available, add the player to the duel.
        if (IsInRadius(playerObj, _combatSigilObjectInfo.m_radius) && !_entitiesInRange.ContainsKey(playerObj)) {
            _entitiesInRange.Add(playerObj, playerActor);

            if (IsSlotAvailable(CombatTeam.Player)) {
                AddParticipant(playerObj, playerActor);
            }
        } 
        else if (!IsInRadius(playerObj, _combatSigilObjectInfo.m_radius) && _entitiesInRange.ContainsKey(playerObj)) {
            _entitiesInRange.Remove(playerObj);
        }
    }

    public override void OnCreatureMove(CoreObject creature, IActorRef suspect, ZoneEntity entity) {
        if (!_isActive) {
            return;
        }

        // Check if the creature is now in range of the object.
        // If there's a slot available, add the creature to the duel.
        if (IsInRadius(creature, _combatSigilObjectInfo.m_radius) && !_entitiesInRange.ContainsKey(creature)) {
            _entitiesInRange.Add(creature, suspect);

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
        else if (!IsInRadius(creature, _combatSigilObjectInfo.m_radius) && _entitiesInRange.ContainsKey(creature)) {
            _entitiesInRange.Remove(creature);
        }
    }

    internal void ZoneBroadcast(IMessage message) => Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
        Selfless = false,
        Sender = Self,
        Message = message
    });

    internal void DuelBroadcast(IMessage message) 
        => EnactActionOnSubCircles(circle => circle.ParticipantActor.Tell(message));

    internal void CreatureBroadcast(IMessage message) => EnactActionOnSubCircles(circle => {
        if (circle.OccupiedTeam == CombatTeam.Monster) {
            circle.ParticipantActor.Tell(message);
        }
    });

    internal new void PlayerBroadcast(IMessage message) => EnactActionOnSubCircles(circle => {
        if (circle.OccupiedTeam == CombatTeam.Player) {
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

        // Activate the sigil.
        InitializeDuel(message.StartingParticipants);
        _renderComponent.Enable();

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DUEL_GRACE_PERIOD_IN_SECONDS);
        Timers.StartSingleTimer(GRACE_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_NEWROUND(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        Logger.Debug("Duel {0} | New round {1} at {2}",
            Logger.Args(Duel.m_duelID.Full, Duel.m_roundNum, DateTime.Now.ToString("HH:mm:ss")));

        // Add the circles to combat if they are not already.
        AddWaitingCombatParticipants();
        CombatResolver.Reset();
        _awaitingCombatMoves = true;

        // Determine the power pip gain for each participant.
        DoPipGain();

        // Echo the new round message to all actors.
        EnactActionOnSubCircles(circle => circle.ParticipantActor.Tell(message));

        // Pre-planning phase just wants to send who is up first.
        Duel.m_duelPhase = kDuelPhase.kPhase_PrePlanning;
        Duel.m_roundNum++;
        SendCombatPhase((byte) Duel.m_duelPhase);
        SendUpFirst(Duel.m_roundNum);

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
        SendCombatUI(PLANNING_TIME);

        var delay = TimeSpan.FromSeconds(PLANNING_TIME);
        Timers.StartSingleTimer(PLANNING_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_PLANNINGPHASEOVER(), delay);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE))]
    private void ReceiveCombatMove(COMBAT_106_PROTOCOL.MSG_ACTORCOMBATMOVE message) {
        // Find which sub circle this is.
        var caster = SubCircles.FirstOrDefault(x => x.ParticipantActor == message.Actor)
            ?? throw new Exception("Combat move received from an actor that is not in the duel.");

        if (!_awaitingCombatMoves) {
            Logger.Warning("Duel {0} | Slot {1} | Received combat move while not expecting it.",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex));

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

        Logger.Debug("Duel {0} | Round {1} over at {2}",
            Logger.Args(Duel.m_duelID.Full, Duel.m_roundNum, DateTime.Now.ToString("HH:mm:ss")));

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
        // Iterate through dead creature participants and remove them from the duel.
        // Players can be healed and therefore don't need to be removed.
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster && !circle.IsAlive) {
                var removeMsg = new COMBAT_106_PROTOCOL.MSG_COMBATDEATH();
                circle.ParticipantActor.Tell(removeMsg);
            }
        });

        // All spells have been called. Inform the client whether this duel continues or ends.
        Duel.m_duelPhase = kDuelPhase.kPhase_Resolution;
        SendCombatPhase((byte) Duel.m_duelPhase);

        var playersWin = AliveCreatureCount <= 0;
        var creaturesWin = AlivePlayerCount <= 0;
        if (!playersWin && !creaturesWin) {
            // Continue. Start a new round.
            Self.Tell(new COMBAT_106_PROTOCOL.MSG_NEWROUND());

            return;
        }

        EndDuel();
    }

    [MessageHandler(typeof(DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_ENDDUEL))]
    private void DespawnDuel() {
        _isActive = false;
        _renderComponent?.Disable();
        Entity.DespawnObject();
        _entitiesInRange.Clear();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message) {
        // Find the sub circle that the client was in and remove them from the duel.
        var subCircle = SubCircles.FirstOrDefault(x => x.ParticipantActor == Sender);
        if (subCircle is null) {
            return;
        }

        // Handle this as if it were the flee action.
        HandleFleeAction(subCircle);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message) {
        // Find the sub circle that the client was in and remove them from the duel.
        var subCircle = SubCircles.FirstOrDefault(x => x.ParticipantActor == Sender);
        if (subCircle is null) {
            return;
        }

        // Handle this as if it were the flee action.
        HandleFleeAction(subCircle);
    }

    private void InitializeDuel(Dictionary<IActorRef, CoreObject> startingParticipants) {
        Duel = CreateDuelWithDefaults();
        SubCircles = CreateDuelActorSubCircles(_sigilTemplate);
        CombatResolver = new Combat.CombatResolver(Duel, SubCircles);

        // Determine which team goes up first.
        Duel.m_firstTeamToAct = (int) DetermineFirstTeam();

        // When the duel is created, it must be created by two suspects: the player and the creature.
        // The creatures will always be team A and the players will always be team B. Assign the first
        // participants to their respective sub circles.
        var startingCreatureActor = startingParticipants.First(x => x.Value.m_templateID != 1);
        var startingPlayerActor = startingParticipants.First(x => x.Value.m_templateID == 1);
        var startingCreatureObject = startingCreatureActor.Value;
        var startingPlayerObject = startingPlayerActor.Value;

        var availableCreatureSubCircle = GetAvailableSubCircleTeamCreature();
        var availablePlayerSubCircle = GetAvailableSubCircleTeamPlayer();

        if (availableCreatureSubCircle == null || availablePlayerSubCircle == null) {
            Logger.Error("Failed to find available sub circles for duel {0}", Logger.Args(SigilId));

            return;
        }

        AssignParticipantToSubCircle(availableCreatureSubCircle, startingCreatureActor.Key, startingCreatureObject);
        AssignParticipantToSubCircle(availablePlayerSubCircle, startingPlayerActor.Key, startingPlayerObject);

        _isActive = true;

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
                SlotType = subCircles[i].m_locationType == "MonsterCircle" ? CombatSlotType.Creature : CombatSlotType.Player
            };
            subCircleObjs[i] = subCircle;
        }

        return subCircleObjs;
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

    private CombatDuelSubCircle GetAvailableSubCircleTeamPlayer() {
        for (int i = 4; i < 8; i++) {
            if (!SubCircles[i].Occupied) {
                return SubCircles[i];
            }
        }

        return null;
    }

    private bool AssignParticipantToSubCircle(CombatDuelSubCircle subCircle, IActorRef actorRef, CoreObject coreObject) {
        if (subCircle.ParticipantActor != null) {
            return false;
        }

        subCircle.AssignParticipant(actorRef, coreObject);

        return true;
    }

    private void AddParticipant(CoreObject participantObject, IActorRef participantActor) {
        var isPlayer = participantObject.m_templateID == 1;

        var alreadyInDuel = SubCircles.Any(x => x.ParticipantObject == participantObject);
        if (alreadyInDuel) {
            return;
        }

        var subCircle = isPlayer ? GetAvailableSubCircleTeamPlayer() : GetAvailableSubCircleTeamCreature();
        if (subCircle is null) {
            Logger.Warning("Duel {0} | No available sub circles for participant {1}",
                Logger.Args(Duel.m_duelID.Full, participantObject.m_globalID));

            return;
        }

        AssignParticipantToSubCircle(subCircle, participantActor, participantObject);

        Logger.Debug("Duel {0} | Slot {1} | Participant {2} joined",
            Logger.Args(Duel.m_duelID.Full, subCircle.SlotIndex, participantObject.m_debugName));
    }

    private void SendCombatPhase(byte phase) {
        // Determine what sigil slot is up first.
        var upFirstSigilSlot = GetUpFirstSigilSlot();

        // Serialize the up first data and send it to all the combat participants.
        var upFirst = new UpFirstData {
            m_resultType = 122, // Always recorded as 122, per packet captures.
            m_roundNum = Duel.m_roundNum,
            m_upFirst = upFirstSigilSlot,
        };
        if (!_serializer.Serialize(upFirst, _upFirstFlags, out var upFirstData)) {
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

        DuelBroadcast(msg);
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
        var combatUiMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_SHOWCOMBATUI {
            DuelID = SigilId
        };
        var planningMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_SETPLANNINGPHASETIMER {
            DuelID = SigilId,
            Time = planningPhaseTimer,
        };

        PlayerBroadcast(combatUiMsg);
        PlayerBroadcast(planningMsg);
    }

    private void SendCombatStats() => EnactActionOnSubCircles(circle => {
        // Serialize participant stats and send them to the participant, locally.
        var participantStats = circle.ParticipantGameStats;
        var participantCombatsStats = participantStats?.GetCombatGameStats();
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

        DuelBroadcast(msg);
    });

    private void SendCombatHand() {
        // Serialize the combat hand and send it to the participant, locally.
        // We're skipping creatures for now.
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster) {
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
                TreasureCardCount = 0,
                ParticipantID = circle.ParticipantObject.m_globalID,
                HandData = buffer,
            };

            participantActor.Tell(msg);
        });
    }

    private void SendCombatPips() {
        var pips = new CombatPipListObj {
            m_pipList = new List<ParticipantPipData>(),
            m_duelID = SigilId
        };

        EnactActionOnSubCircles(circle => {
            if (!circle.AddedToDuel || !circle.IsAlive) {
                return;
            }

            var genericPips = circle.CombatParticipant.m_pipCount.m_genericPips;
            var powerPips = circle.CombatParticipant.m_pipCount.m_powerPips;
            var participantPipData = new ParticipantPipData {
                m_acq = 1,
                m_partID = (GID) circle.ParticipantObject.m_globalID,
                m_pips = new PipCount() {
                    m_genericPips = genericPips,
                    m_powerPips = powerPips,
                }
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

    private void SendCombatMoveSelection(ulong participantId, byte moveType, Spell spell, byte targetIndex) {
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
        PlayerBroadcast(msg);
    }

    private void HandleDiscardMove(CombatDuelSubCircle caster, int spellSelection) {
        var spell = caster.GetSpellFromLastHand((byte) spellSelection);
        caster.DiscardCard(spell);

        Logger.Debug("Duel {0} | Slot {1} | Discarded a card: {2}",
            Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spell.m_templateID.ToString() ?? "None"));
    }

    private void HandlePassMove(CombatDuelSubCircle caster) {
        // If the participant passes, we don't need to know what spell they were casting.
        CombatResolver.AddCombatMove(CombatMoveType.Pass, caster, null, null);

        if (caster.OccupiedTeam == CombatTeam.Player) {
            SendCombatMoveSelection(caster.ParticipantObject.m_globalID, (byte) CombatMoveType.Pass, null, 0);
        }
    }

    private void HandleAttackMove(CombatDuelSubCircle caster, int spellSelection, uint spellTarget) {
        var spell = caster.GetSpellFromLastHand((byte) spellSelection);
        if (!caster.HasPipsForSpell(spell)) {
            Logger.Warning("Duel {0} | Slot {1} | Participant does not have enough pips for spell {2}",
                Logger.Args(Duel.m_duelID.Full, caster.SlotIndex, spell.m_templateID));

            CombatResolver.AddCombatMove(CombatMoveType.Pass, caster, null, null);
        }

        // Find the target sub circle. Valid targets are 1-8.
        // If the spell doesn't have a target like for AoE spells or self-heals,
        // the value will be the integer cap.
        var target = caster;
        if (spellTarget >= 0 && spellTarget < SubCircles.Length) {
            target = SubCircles[spellTarget];
        }

        CombatResolver.AddCombatMove(CombatMoveType.Attack, caster, target, spell);

        if (caster.OccupiedTeam == CombatTeam.Player) {
            SendCombatMoveSelection(caster.ParticipantObject.m_globalID, (byte) CombatMoveType.Attack, spell, (byte) spellTarget);
        }
    }

    private void HandleChangeMindAction(CombatDuelSubCircle caster) {
        // Send the action director a null spell to indicate that the participant has changed their mind.
        CombatResolver.AddCombatMove(CombatMoveType.ChangeMind, caster, null, null);

        // Echo the change mind action to each player participant
        if (caster.OccupiedTeam == CombatTeam.Player) {
            SendCombatMoveSelection(caster.ParticipantObject.m_globalID, (byte) CombatMoveType.ChangeMind, null, 0);
        }
    }

    private void HandleFleeAction(CombatDuelSubCircle caster) {
        var actor = caster.ParticipantActor;
        var participantObjId = caster.ParticipantObject.m_globalID;

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

    private byte GetUpFirstSigilSlot() {
        var upFirstSigilSlot = 0;

        // Define the relevant CombatSlotType
        var targetType = Duel.m_firstTeamToAct == (int) CombatTeam.Player
                                     ? CombatSlotType.Player
                                     : CombatSlotType.Creature;

        for (int i = 0; i < SubCircles.Length; i++) {
            if (SubCircles[i].SlotType == targetType && SubCircles[i].IsAlive) {
                upFirstSigilSlot = SubCircles[i].SlotIndex;
                break;
            }
        }

        return (byte) upFirstSigilSlot;
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

        Logger.Debug("Duel {0} | Slot {1} | Serialized participant sent", Logger.Args(Duel.m_duelID, circle.SlotIndex));

        circle.AddedToDuel = true;
        Duel.m_flatParticipantList.Add(participant);
    });

    private void DoPipGain() => EnactActionOnSubCircles(circle => {
        if (!circle.AddedToDuel || !circle.IsAlive) {
            return;
        }

        circle.DoPipGain();
    });

    private void EndDuel() {
        // The duel has ended. Inform the clients of the result.
        var playersWin = AliveAndInDuelCreatureCount <= 0;
        var creaturesWin = AliveAndInDuelPlayerCount <= 0;

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
    }

    private void PlayerWin() {
        Logger.Debug("Duel {0} | Duel ended. Players win.", Logger.Args(Duel.m_duelID.Full));

        Duel.m_duelPhase = kDuelPhase.kPhase_Victory;
        SendCombatPhase((byte) Duel.m_duelPhase);

        // Send the final messages to the participants.
        var combatVictoryMsg = new DOODLEDOUG_MESSAGES_51_PROTOCOL.MSG_COMBATVICTORY();
        EnactActionOnSubCircles(circle => {
            if (circle.OccupiedTeam == CombatTeam.Monster) {
                return;
            }

            circle.ParticipantActor.Tell(combatVictoryMsg);

            var victoryMsg = new COMBAT_106_PROTOCOL.MSG_COMBATWIN();
            circle.ParticipantActor.Tell(victoryMsg);
        });
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
        if (circle.OccupiedTeam != CombatTeam.Player) {
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
   
    private bool IsSlotAvailable(CombatTeam team) {
        var IsNewbieZone = false;
        var IsDangerousZone = false;

        // Newbie zones (like Unicorn Way) can only have 1 creature as a base.
        // Dangerous zones (like Sunken City) can have 3 creatures as a base.
        // Every player in the duel also allocates 1 more creature slot.
        var baseCreatureCount = IsNewbieZone ? 1 : IsDangerousZone ? 3 : 2;
        var maxCreatures = baseCreatureCount + (PlayerCount - 1);

        var slotAvailable = (team == CombatTeam.Player)
            ? PlayerCount < 4
            : CreatureCount < 4 && (CreatureCount < maxCreatures);

        return slotAvailable;
    }

}