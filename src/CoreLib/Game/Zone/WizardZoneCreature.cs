/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// An extension of <see cref="WizardZoneObject" /> that adds implementations to move along
/// a given <see cref="WizardZonePath" />.
/// </summary>
public class WizardZoneCreature : WizardZoneObject, IWithTimers {
    protected const int MINIMUM_MOVEMENT_DELAY_IN_SECONDS = 1;
    protected const int MOVEMENT_INTERVAL_START_DELAY_IN_SECONDS = 1;
    protected const int LOCATION_UPDATE_INTERVAL = 1;

    public enum CreatureState {
        Stopped,
        Wandering,
        Combat
    }

    public ServerWizGameStats GameStats { get; private set; }
    public ITimerScheduler Timers { get; set; }

    protected readonly WizardZonePath _path;
    protected readonly TimeSpan _startingDelay = TimeSpan.FromSeconds(MOVEMENT_INTERVAL_START_DELAY_IN_SECONDS);
    protected readonly TimeSpan _minimumMovementIntervalDelay = TimeSpan.FromSeconds(MINIMUM_MOVEMENT_DELAY_IN_SECONDS);
    protected readonly TimeSpan _fishInteractionInterval = TimeSpan.FromSeconds(LOCATION_UPDATE_INTERVAL);
    protected readonly NodeObject[] _nodes;
    protected CreatureState _creatureState;
    protected byte _targetNodeIndex;
    protected bool _justPaused;
    protected DateTime _lastMoveTime;
    protected ServerNPCBehavior _npcBehavior;
    protected ServerPathBehavior _pathBehavior;
    protected IActorRef _combatAiActor;

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

        CreateBehaviorsFromTemplate();
        SpawnSelf();

        // Start the fish interaction interval.
        var fishIntervalMsg = new ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL();
        Timers.StartPeriodicTimer("fishInteractionInterval", fishIntervalMsg, TimeSpan.Zero,  _fishInteractionInterval);

        if (IsMovingCreature()) {
            _creatureState = CreatureState.Wandering;

            // Start the movement interval.
            var movementIntervalMsg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
            Timers.StartSingleTimer("movementInterval", movementIntervalMsg, _startingDelay);
        }
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
            NewState = (sbyte) (_creatureState == CreatureState.Wandering ? 1 : 0)
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
        if (_creatureState == CreatureState.Combat || !IsDuelingCreature()) {
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
        if (_pathBehavior is null || !IsMovingCreature() || _creatureState != CreatureState.Wandering) {
            return ActiveGameObject.m_location;
        }

        // If the creature is moving, then calculate the position based on the
        // last node reached, the target node position, and the movement speed.
        var lastNodeReached = ActiveGameObject.m_location;
        var targetNode = CurrentTargetNode.m_location;
        var totalDistance = Vector3.Distance(lastNodeReached, targetNode);
        var elapsedTimeInSeconds = (DateTime.Now - _lastMoveTime).TotalSeconds;
        var distanceTraveled = elapsedTimeInSeconds * _pathBehavior.MovementSpeed * _pathBehavior.MovementMultiplier;

        if (distanceTraveled >= totalDistance) {
            return targetNode;
        }

        var t = distanceTraveled / totalDistance;
        var x = lastNodeReached.X + t * (targetNode.X - lastNodeReached.X);
        var y = lastNodeReached.Y + t * (targetNode.Y - lastNodeReached.Y);
        var z = lastNodeReached.Z + t * (targetNode.Z - lastNodeReached.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }

    protected void Die() {
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

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void ReceiveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        StartCombat(message.Duel, message.SubCircle);

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
        if (this.GameStats is null) {
            Logger.Error("Creature {0} was sourced for game stats, but no {1} is set.",
                Logger.Args(ActiveGameObject.m_globalID, nameof(ServerWizGameStats)));
            return;
        }
        if (!TryGetBehavior<ServerNPCBehavior>(out var _npcBehavior)) {
            Logger.Error("Creature {0} was sourced for game stats, but no {1} is in the list of behaviors.",
                Logger.Args(ActiveGameObject.m_globalID, nameof(ServerNPCBehavior)));
            return;
        }
        if (!TryGetBehavior<ServerCreatureSpellbookBehavior>(out var _spellbookBehavior)) {
            Logger.Error("Creature {0} was sourced for game stats, but no {1} is in the list of behaviors.",
                Logger.Args(ActiveGameObject.m_globalID, nameof(ServerCreatureSpellbookBehavior)));
            return;
        }

        var msg = new COMBAT_106_PROTOCOL.MSG_CREATURESTATS {
            GameStats = this.GameStats,
            CombatIntelligence = _npcBehavior.Intelligence,
            CombatSelfishFactor = _npcBehavior.SelfishFactor,
            CombatAggressionFactor = _npcBehavior.AggressiveFactor,
            CombatLevel = _npcBehavior.Level,
            MagicSchool = _npcBehavior.School,
            SpellList = _spellbookBehavior.SpellList,
        };
        Sender.Tell(msg);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewCombatRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        _combatAiActor.Forward(message);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATDEATH))]
    private void ReceiveCombatDeath(COMBAT_106_PROTOCOL.MSG_COMBATDEATH message) {
        if (_combatAiActor is not null) {
            _combatAiActor.Forward(message);
        }

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
            var pauseDelay = TimeSpan.FromSeconds(_pathBehavior.PauseTime);
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
        var travelTimeInSeconds = distance / (_pathBehavior.MovementSpeed * _pathBehavior.MovementMultiplier);
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

    private void CreateBehaviorsFromTemplate() {
        var pathBehaviorTemplate = Template.m_behaviors
            .FirstOrDefault(x => x is PathBehaviorTemplate) as PathBehaviorTemplate;
        var pathMovementBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is PathMovementBehaviorTemplate) as PathMovementBehaviorTemplate;
        var duelistBehaviorTemplate = Template.m_behaviors
            .FirstOrDefault(x => x is DuelistBehaviorTemplate) as DuelistBehaviorTemplate;
        var npcBehaviorTemplate = Template.m_behaviors
            .FirstOrDefault(x => x is NPCBehaviorTemplate) as NPCBehaviorTemplate;
        var equipmentBehaviorTemplate = Template.m_behaviors
            .FirstOrDefault(x => x is EquipmentBehaviorTemplate) as EquipmentBehaviorTemplate;

        CreatePathBehavior(pathBehaviorTemplate, pathMovementBehavior);
        CreateNPCBehavior(npcBehaviorTemplate, duelistBehaviorTemplate);
        CreateEquipmentBehavior(equipmentBehaviorTemplate);
    }

    private void CreatePathBehavior(PathBehaviorTemplate pathTemplate, PathMovementBehaviorTemplate movementTemplate) {
        if (pathTemplate is null) {
            return;
        }

        var pathBehaviorInstance = new ServerPathBehavior {
            PathType = pathTemplate.m_kPathType,
            PathId = pathTemplate.m_pathID,
            PathDirection = pathTemplate.m_nPathDirection,
            Actions = pathTemplate.m_actionList,
            PauseChance = pathTemplate.m_pauseChance,
            PauseTime = pathTemplate.m_timeToPause
        };

        if (movementTemplate is not null) {
            pathBehaviorInstance.MovementSpeed = movementTemplate.m_movementSpeed;
            pathBehaviorInstance.MovementMultiplier = movementTemplate.m_movementScale;
        }
        else {
            Logger.Error("PathMovementBehaviorTemplate not found for creature {0}.",
                Logger.Args(ActiveGameObject.m_globalID));
        }

        this.Behaviors.Add(pathBehaviorInstance);
        this._pathBehavior = pathBehaviorInstance;
    }

    private void CreateNPCBehavior(NPCBehaviorTemplate npcTemplate, DuelistBehaviorTemplate duelistTemplate) {
        if (npcTemplate is null) {
            return;
        }

        InteractionRadius = duelistTemplate?.m_npcProximity ?? base.InteractionRadius;

        // Try to parse the npcBehaviorTemplate.m_schoolOfFocus to a MagicSchool.
        var school = MagicSchool.Balance;
        if (npcTemplate.m_schoolOfFocus != "" && !Enum.TryParse(npcTemplate.m_schoolOfFocus, out school)) {
            Logger.Error("Failed to parse magic school {0} for creature {1}.",
                Logger.Args(npcTemplate.m_schoolOfFocus, ActiveGameObject.m_globalID));
            return;
        }

        var npcBehaviorInstance = new ServerNPCBehavior {
            BossMob = npcTemplate.m_bossMob,
            Intelligence = npcTemplate.m_fIntelligence,
            SelfishFactor = npcTemplate.m_fSelfishFactor,
            AggressiveFactor = npcTemplate.m_nAggressiveFactor,
            StartingHealth = npcTemplate.m_nStartingHealth,
            School = school,
            Level = npcTemplate.m_nLevel,
            TurnTowardsPlayer = npcTemplate.m_turnTowardsPlayer,
            IsMonster = duelistTemplate is not null,
        };
        this.Behaviors.Add(npcBehaviorInstance);
        this._npcBehavior = npcBehaviorInstance;

        this.GameStats = new ServerWizGameStats(school, npcTemplate.m_nLevel) {
            m_currentHitpoints = npcTemplate.m_nStartingHealth,
            m_baseHitpoints = npcTemplate.m_nStartingHealth,
        };

        // Boss mobs have a number of base effects.
        if (npcTemplate.m_baseEffects.Count > 0) {
            foreach (var effect in npcTemplate.m_baseEffects) {
                CharacterEffectHelper.AddGameEffectToStats(GameStats, effect);
            }
        }
    }

    private void CreateEquipmentBehavior(EquipmentBehaviorTemplate equipmentTemplate) {
        if (equipmentTemplate is null) {
            return;
        }

        var equipmentBehaviorInstance = new ServerWizEquipmentBehavior {
            EquippedItems = new List<WizClientObjectItem>(),
            SlotList = new List<WizardData.Models.Player.EquipmentSlot>(),
            EquippedItemIds = new List<ulong>()
        };

        this.Behaviors.Add(equipmentBehaviorInstance);

        foreach (var itemTemplateId in equipmentTemplate.m_itemList) {
            var template = (WizItemTemplate) CoreObjectFactory.GetCoreTemplate(itemTemplateId);
            if (template is null) {
                Logger.Error("Failed to get item template {0} for creature {1}.",
                    Logger.Args(itemTemplateId, ActiveGameObject.m_globalID));
                continue;
            }

            // Check if the item has a deck behavior.
            var deckBehaviorTemplate = template.m_behaviors
                .FirstOrDefault(x => x is DeckBehaviorTemplate) as DeckBehaviorTemplate;
            if (deckBehaviorTemplate is not null) {
                CreateDeckBehavior(deckBehaviorTemplate);
            }

            EquipItem(template);
        }
    }

    private void CreateDeckBehavior(DeckBehaviorTemplate deckBehaviorTemplate) {
        var deckBehaviorInstance = new ServerCreatureSpellbookBehavior(deckBehaviorTemplate);
        this.Behaviors.Add(deckBehaviorInstance);
    }

    private void EquipItem(WizItemTemplate template) {
        if (!TryGetBehavior<ServerWizEquipmentBehavior>(out var equipmentBehavior)) {
            Logger.Warning("Creature {0} tried to equip item {1} but has no equipment behavior.",
                Logger.Args(ActiveGameObject.m_globalID, template.m_templateID));
            return;
        }

        var item = (WizClientObjectItem) CoreObjectFactory.FinalizeCoreObject(template.m_templateID);
        equipmentBehavior.ForceEquipItem(item);

        CharacterEffectHelper.AddEffectsToGameStats(GameStats, template);
    }

    private void StartCombat(CombatDuelActor duel, CombatDuelActorSubCircle subCircle) {
        CreateCombatAIActor(duel, subCircle);
        StopMovement();
        _creatureState = CreatureState.Combat;
    }

    private void CreateCombatAIActor(CombatDuelActor duel, CombatDuelActorSubCircle subCircle) {
        if (_combatAiActor is not null) {
            return;
        }

        var props = CombatAIActor.Props(Self, duel, subCircle);
        _combatAiActor = Context.ActorOf(props, $"combatAIActor_{ActiveGameObject.m_globalID}");
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

    private bool IsMovingCreature() {
        if (TryGetBehavior<ServerPathBehavior>(out var pathBehavior)) {
            return pathBehavior.MovementSpeed > 0.0f && _nodes.Length > 1;
        }

        return false;
    }

    private bool IsDuelingCreature() {
        if (_npcBehavior is not null && _npcBehavior.IsMonster) {
            return true;
        }

        return false;
    }

    private bool ShouldPause() {
        if (_pathBehavior is null) {
            return false;
        }

        return _pathBehavior.PauseChance > 0 && new Random().Next(0, 100) < _pathBehavior.PauseChance;
    }

    private NodeObject CurrentTargetNode => _nodes[_targetNodeIndex];
}
