/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// An extension of <see cref="WizardZoneObject" /> that adds implementations to move along
/// a given <see cref="WizardZonePath" />.
/// </summary>
public class WizardZoneCreature : WizardZoneObject, IWithTimers {
    private const int MINIMUM_MOVEMENT_DELAY_IN_SECONDS = 1;
    private const int MOVEMENT_INTERVAL_START_DELAY_IN_SECONDS = 1;
    private const int LOCATION_UPDATE_INTERVAL = 1;

    internal enum CreatureState {
        Stopped,
        Wandering,
        Combat
    }

    public float CombatIntelligence { get; private set; }
    public float CombatSelfishFactor { get; private set; }
    public float CombatAggressiveFactor { get; private set; }
    public int CombatLevel { get; private set; }
    public int StartingHealth { get; private set; }
    public WizGameStats GameStats { get; private set; }
    public ITimerScheduler Timers { get; set; }

    private readonly WizardZonePath _path;
    private readonly TimeSpan _startingDelay = TimeSpan.FromSeconds(MOVEMENT_INTERVAL_START_DELAY_IN_SECONDS);
    private readonly TimeSpan _minimumMovementIntervalDelay = TimeSpan.FromSeconds(MINIMUM_MOVEMENT_DELAY_IN_SECONDS);
    private readonly TimeSpan _fishInteractionInterval = TimeSpan.FromSeconds(LOCATION_UPDATE_INTERVAL);
    private readonly NodeObject[] _nodes;
    private CreatureState _creatureState;
    private byte _targetNodeIndex;
    private float _movementSpeed = 0.0f;
    private float _movementSpeedMultiplier = 1.0f;
    private uint _pauseChance = 0;
    private float _pauseTime = 6.0f;
    private bool _justPaused;
    private DateTime _lastMoveTime;
    private bool _isMovingCreature;
    private bool _isDuelingCreature;

    // ctor
    public WizardZoneCreature(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef)
            : base(activeGameObject, template, wizardZoneRef) {
        this._path = path;
        this._nodes = path.Nodes.Keys.ToArray();
        this._targetNodeIndex = startingNodeIndex;
        this._creatureState = CreatureState.Stopped;

        SetPropertiesFromTemplate();

        if (!this._isMovingCreature) {
            return;
        }

        _creatureState = CreatureState.Wandering;

        // Start the movement interval.
        var msg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
        Timers.StartSingleTimer("movementInterval", msg, _startingDelay);

        // Start the fish interaction interval.
        var msg2 = new ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL();
        Timers.StartPeriodicTimer("fishInteractionInterval", msg2, TimeSpan.Zero,  _fishInteractionInterval);
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef) {
        return Akka.Actor.Props.Create(()
            => new WizardZoneCreature(activeGameObject, template, path, startingNodeIndex, wizardZoneRef));
    }

    protected override void OnPlayerJoin(CoreObject player, IActorRef playerActor) {
        // Since we're not constantly updating the position of the game object, we need to
        // update the position of the game object when a player joins.
        ActiveGameObject.m_location = GetPosition();
        base.OnPlayerJoin(player, playerActor);

        // Inform the new player that this creature is moving.
        // MOVESTATE: 0 = stopped, 1 = moving.
        var msgMoveState = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            GlobalID = ActiveGameObject.m_globalID,
            NewState = (sbyte) (_creatureState == CreatureState.Wandering ? 0 : 1)
        };
        playerActor.Tell(msgMoveState);

        // Inform the new player which node we might be moving to.
        if (_creatureState == CreatureState.Wandering) {
            var msgServerMove = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
                Direction = (byte) (CurrentTargetNode.m_direction / Math.PI / 2 * 250),
                LocationX = (ushort) (CurrentTargetNode.m_location.X / 4.0f),
                LocationY = (ushort) (CurrentTargetNode.m_location.Y / 4.0f),
                LocationZ = (ushort) (CurrentTargetNode.m_location.Z / 4.0f),
                MobileID = ActiveGameObject.m_nMobileID
            };
            playerActor.Tell(msgServerMove);
        }
    }

    protected override void OnPlayerInteractionEnter(CoreObject suspectObject, IActorRef suspectActor) {
        // If I'm a hostile creature and a player just provoked me, then start a duel.
        if (_creatureState == CreatureState.Combat || !_isDuelingCreature) {
            return;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
            StartingParticipants = new Dictionary<IActorRef, CoreObject>
            {
                { suspectActor, suspectObject },
                { Self, ActiveGameObject }
            }
        };
        WizardZoneRef.Tell(msg);
    }

    protected override Vector3 GetPosition() {
        // If the creature is not moving, then return the current position.
        if (!_isMovingCreature || _creatureState != CreatureState.Wandering) {
            return ActiveGameObject.m_location;
        }

        // If the creature is moving, then calculate the position based on the
        // last node reached, the target node position, and the movement speed.
        var lastNodeReached = ActiveGameObject.m_location;
        var targetNode = CurrentTargetNode.m_location;
        var totalDistance = Vector3.Distance(lastNodeReached, targetNode);
        var elapsedTimeInSeconds = (DateTime.Now - _lastMoveTime).TotalSeconds;
        var distanceTraveled = elapsedTimeInSeconds * _movementSpeed * _movementSpeedMultiplier;

        if (distanceTraveled >= totalDistance) {
            return targetNode;
        }

        var t = distanceTraveled / totalDistance;
        var x = lastNodeReached.X + t * (targetNode.X - lastNodeReached.X);
        var y = lastNodeReached.Y + t * (targetNode.Y - lastNodeReached.Y);
        var z = lastNodeReached.Z + t * (targetNode.Z - lastNodeReached.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void ReceiveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        StartCombat();

        ActiveGameObject.m_location = message.SlotPosition;

        // Set the orientation of the creature to the orientation of the slot.
        // The orientation is in radians, so convert it to degrees.
        var orientationInRadians = message.SlotOrientation;
        var orientationInDegrees = orientationInRadians * (180 / Math.PI);
        var orientationVector = new Vector3(0, 0, (float) orientationInDegrees);
        ActiveGameObject.m_orientation = orientationVector;
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS))]
    private void ReceiveQueryGameStats(COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS message) {
        var msg = new COMBAT_106_PROTOCOL.MSG_CREATURESTATS {
            GameStats = this.GameStats,
            CombatIntelligence = this.CombatIntelligence,
            CombatSelfishFactor = this.CombatSelfishFactor,
            CombatAggressionFactor = this.CombatAggressiveFactor,
            CombatLevel = this.CombatLevel
        };
        Sender.Tell(msg);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATDEATH))]
    private void ReceiveCombatDeath(COMBAT_106_PROTOCOL.MSG_COMBATDEATH message) {
        // This creature has been defeated in a duel.
        Die();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL))]
    private void ReceiveMoveInterval(ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL message) {
        if (_creatureState != CreatureState.Wandering) {
            return;
        }

        if (CurrentTargetNode is not null) {
            UpdateGameObjectPosition(CurrentTargetNode);
        }

        // Creatures have a chance to pause at each node. This stops them from clumping together.
        // If the creature is already paused, then don't pause again.
        if (ShouldPause() && !_justPaused) {
            var pauseDelay = TimeSpan.FromSeconds(_pauseTime);
            var pauseMsg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
            Timers.StartSingleTimer("movementInterval", pauseMsg, pauseDelay);
            _justPaused = true;

            return;
        }
        _justPaused = false;

        // Target a new node.
        _targetNodeIndex = GetNextNodeIndex();

        var currentPosition = ActiveGameObject.m_location;
        var distance = Vector3.Distance(currentPosition, CurrentTargetNode.m_location);

        // If the distance is zero, then the mob is already at the target node.
        if (distance == 0) {
            var msg2 = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
            Timers.StartSingleTimer("movementInterval", msg2, _minimumMovementIntervalDelay);

            return;
        }

        // Calculate the delay based on the distance between the current position
        // and the target node position, and the movement speed of the creature.
        var travelTimeInSeconds = distance / (_movementSpeed * _movementSpeedMultiplier);
        var travelTimeInMilli = (int) travelTimeInSeconds * 1000;

        // Clamp the delay to a minimum.
        if (travelTimeInMilli < 500) {
            travelTimeInMilli = 500;
        }

        // Begin traveling to the target node.
        _lastMoveTime = DateTime.Now;
        BroadcastMovement(CurrentTargetNode);

        // Call the movement interval again after the delay.
        var delay = TimeSpan.FromMilliseconds(travelTimeInMilli);
        var msg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
        Timers.StartSingleTimer("movementInterval", msg, delay);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL))]
    private void ReceiveFishInteractionInterval(ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL message) {
        if (_creatureState == CreatureState.Combat) {
            return;
        }

        ActiveGameObject.m_location = GetPosition();

        var msg = new ZONE_102_PROTOCOL.MSG_FISHINTERACTION() {
            CoreObject = ActiveGameObject,
            Suspect = Self,
            IsCreature = true
        };
        WizardZoneRef.Tell(msg);
    }

    private void SetPropertiesFromTemplate() {
        var pathBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is PathBehaviorTemplate) as PathBehaviorTemplate;
        var pathMovementBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is PathMovementBehaviorTemplate) as PathMovementBehaviorTemplate;
        var duelistBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is DuelistBehaviorTemplate) as DuelistBehaviorTemplate;
        var npcBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is NPCBehaviorTemplate) as NPCBehaviorTemplate;

        if (pathBehavior is not null) {
            this._pauseChance = pathBehavior.m_pauseChance;
            this._pauseTime = pathBehavior.m_timeToPause;
        }

        if (pathMovementBehavior is not null) {
            this._movementSpeed = pathMovementBehavior.m_movementSpeed;
            this._movementSpeedMultiplier = pathMovementBehavior.m_movementScale;
            this._isMovingCreature = true && pathMovementBehavior.m_movementSpeed > 0.0f;
        }

        if (duelistBehavior is not null) {
            base.InteractionRadius = duelistBehavior.m_npcProximity;
            this._isDuelingCreature = true;
        }

        if (npcBehavior is not null) {
            this.CombatIntelligence = npcBehavior.m_fIntelligence;
            this.CombatSelfishFactor = npcBehavior.m_fSelfishFactor;
            this.CombatAggressiveFactor = npcBehavior.m_nAggressiveFactor;
            this.CombatLevel = npcBehavior.m_nLevel;
            this.StartingHealth = npcBehavior.m_nStartingHealth;

            // todo: source other game stats like resistences here. Unsure if client ships with this information.
            this.GameStats = new WizGameStats {
                m_currentHitpoints = this.StartingHealth,
                m_baseHitpoints = this.StartingHealth,
            };
        }

        if (CoreObjectFactory.FindBehaviorInstance<NPCBehavior>(ActiveGameObject, out var behavior)) {
            behavior.m_isMonster = this._isDuelingCreature;
        }
    }

    private void StartCombat() {
        StopMovement();
        _creatureState = CreatureState.Combat;
    }

    private void StopMovement() {
        _creatureState = CreatureState.Stopped;

        // Broadcast the new move state to all players.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_MOVESTATE {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            },
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMsg);
    }

    private byte GetNextNodeIndex() {
        if (_targetNodeIndex + 1 >= _nodes.Length) {
            return 0;
        }

        return unchecked((byte) (_targetNodeIndex + 1));
    }

    private void BroadcastMovement(NodeObject targetNode) {
        // Broadcast the new move state to all players.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_MOVESTATE {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            },
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMsg);

        var msg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
                // Compress fields by a factor of 4.
                Direction = (byte) (targetNode.m_direction / Math.PI / 2 * 250),
                LocationX = (ushort) (targetNode.m_location.X / 4.0f),
                LocationY = (ushort) (targetNode.m_location.Y / 4.0f),
                LocationZ = (ushort) (targetNode.m_location.Z / 4.0f),
                MobileID = ActiveGameObject.m_nMobileID
            }
        };
        WizardZoneRef.Tell(msg);
    }

    private void UpdateGameObjectPosition(NodeObject targetNode) {
        ActiveGameObject.m_location = new Vector3(
            targetNode.m_location.X,
            targetNode.m_location.Y,
            targetNode.m_location.Z);
    }

    private bool ShouldPause() {
        return _pauseChance > 0 && new Random().Next(0, 100) < _pauseChance;
    }

    private void Die() {
        // Broadcast the death of this creature to all players.
        var broadcastMSg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
                GameObjectID = ActiveGameObject.m_globalID
            },
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMSg);

        // Inform the path that this creature has died.
        _path.RemoveCreature(ActiveGameObject.m_templateID);

        Context.Stop(Self);
    }

    private NodeObject CurrentTargetNode => _nodes[_targetNodeIndex];
}
