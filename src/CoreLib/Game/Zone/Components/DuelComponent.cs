/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Sigils;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class DuelComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const byte PLANNING_TIME = 30;
    private const float DUEL_GRACE_PERIOD_IN_SECONDS = 3.75f;
    private const float DUEL_NEW_ROUND_DELAY = 2.5f;
    private const float YAW_ERROR_COMPENSATION = 1.58f;
    private const string GRACE_TIME_KEY = "GracePeriod";
    private const string PLANNING_TIME_KEY = "PlanningPhase";
    private const string PREPLANNING_TIME_KEY = "PrePlanningPhase";

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

    private CombatSigilObjectInfo _combatSigilObjectInfo;
    private RenderComponent _renderComponent;
    private CombatSigilTemplate _sigilTemplate;
    private bool _isActive;
    private int _playerCount;
    private int _creatureCount;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is not null && x.m_behaviorName == "DuelBehavior");

    public override void OnStart() {
        // Disable the RenderComponent. We'll activate it when the sigil is activated.
        _renderComponent = Entity.GetComponentOfType<RenderComponent>();
        _renderComponent?.Disable();

        // Get the sigil template.
        _sigilTemplate = (CombatSigilTemplate) SigilFactory.GetSigilTemplate(_combatSigilObjectInfo.m_sigilType);
    }

    internal void ZoneBroadcast(IMessage message) => Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST {
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
    private void ReceiveSigilDetails(ZONE_102_PROTOCOL.MSG_SIGILDETAILS message)
        => _combatSigilObjectInfo = message.CombatSigilObjectInfo;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveDuelStart(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        if (_isActive) {
            Logger.Warning("Received start request for already active sigil {0}",
                Logger.Args(Entity.ActiveGameObject.m_globalID));

            return;
        }

        // Activate the sigil.
        _renderComponent.Enable();
        InitializeDuel(message.StartingParticipants);

        // Fire a message to self to start the duel after the grace period has ended.
        var delay = TimeSpan.FromSeconds(DUEL_GRACE_PERIOD_IN_SECONDS);
        Timers.StartSingleTimer(GRACE_TIME_KEY, new COMBAT_106_PROTOCOL.MSG_NEWROUND(), delay);
    }

    [MessageHandler(typeof(WIZARDCOMBAT_51_PROTOCOL.MSG_ENDDUEL))]
    private void ReceiveDuelEnd(WIZARDCOMBAT_51_PROTOCOL.MSG_ENDDUEL message) {
        _isActive = false;
        _renderComponent?.Disable();

        // Cleanup and remove the sigil entity
        var removeMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };

        Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEPLAYERBROADCAST {
            Message = removeMsg,
            Selfless = false
        });
    }

    private void InitializeDuel(Dictionary<IActorRef, CoreObject> startingParticipants) {
        Duel = CreateDuelWithDefaults();
        CombatResolver = new Combat.CombatResolver(Duel, SubCircles);
        SubCircles = CreateDuelActorSubCircles(_sigilTemplate);

        // Determine which team goes up first.
        Duel.m_firstTeamToAct = (int) DetermineFirstTeam();

        var startingCreatureActor = startingParticipants.FirstOrDefault(x => x.Value.m_templateID != 1);
        var startingPlayerActor = startingParticipants.FirstOrDefault(x => x.Value.m_templateID == 1);
        var startingCreatureObject = startingCreatureActor.Value;
        var startingPlayerObject = startingPlayerActor.Value;

        var availableCreatureSubCircles = GetAvailableSubCircleTeamCreature();
        var availablePlayerSubCircles = GetAvailableSubCircleTeamPlayer();

        if (availableCreatureSubCircles == null || availablePlayerSubCircles == null) {
            Logger.Error("Failed to find available sub circles for duel {0}", Logger.Args(SigilId));
            return;
        }

        AssignParticipantToSubCircle(availableCreatureSubCircles, startingCreatureActor.Key, startingCreatureObject);
        AssignParticipantToSubCircle(availablePlayerSubCircles, startingPlayerActor.Key, startingPlayerObject);

        Logger.Debug("Duel {0} | Created. Grace period over in {1}", 
            Logger.Args(Duel.m_duelID, DUEL_GRACE_PERIOD_IN_SECONDS));
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

        var team = coreObject.m_templateID == 1 ? CombatTeam.Player : CombatTeam.Monster;
        if (team == CombatTeam.Monster) {
            _creatureCount++;
        }
        else {
            _playerCount++;
        }

        subCircle.AssignParticipant(actorRef, coreObject);

        return true;
    }

    private static CombatTeam DetermineFirstTeam() 
        => (CombatTeam) new Random().Next(0, 2);

}