/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class RenderComponent(ZoneEntity entity) : BaseZoneComponent(entity), IComponentFactory {

    private readonly CoreObjectSerializer _serializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask(SerializerOptions.PropertyFlags.Public
                | SerializerOptions.PropertyFlags.Transmit
                | SerializerOptions.PropertyFlags.AuthorityTransmit);
    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private float _renderDistance;
    private bool _doesDistanceCheck = false;

    public override void OnStart() {
        // Check if the object should be spawned based on distance.
        _doesDistanceCheck = entity.Template.m_behaviors
                .OfType<AnimationBehaviorTemplate>()
                .Any(anim => anim.m_bFadesIn || anim.m_bFadesOut);

        _renderDistance = Entity.Zone.ZoneData.m_farClip;

        // Broadcast the creation of the object to all players.
        var clientObj = Entity.GetClientBehaviorInstance();
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = _serializer.Serialize(clientObj)
        });
    }

    public static bool ShouldAttachToEntity(CoreTemplate template) =>
        template is GameObjectTemplate and not DynamicTriggerTemplate;

    public override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        if (!_doesDistanceCheck) {
            // If the object does not have a distance check, spawn the object for the player.
            SpawnObjectForPlayer(suspect);
            return;
        }

        // If the player joins within render distance, spawn the object for them.
        if (IsInRadius(player, _renderDistance)) {
            _playersInRange.Remove(player);
            SpawnObjectForPlayer(suspect);
            _playersInRange.Add(player, suspect);
        }
    }

    public override void OnPlayerLeave(IActorRef suspect, ulong id) {
        // Remove the player from the list of players in range.
        var player = _playersInRange.FirstOrDefault(x => x.Value == suspect).Key;
        if (player != null) {
            _playersInRange.Remove(player);
        }

        DespawnObjectForPlayer(suspect);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) {
        if (!_doesDistanceCheck) {
            return;
        }

        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, _renderDistance) && !_playersInRange.ContainsKey(playerObj)) {
            // If the player is in range, spawn the object for them.
            SpawnObjectForPlayer(playerActor);
            _playersInRange.Add(playerObj, playerActor);
        }
        else if (!IsInRadius(playerObj, _renderDistance) && _playersInRange.ContainsKey(playerObj)) {
            // If the player is out of range, despawn the object for them.
            DespawnObjectForPlayer(playerActor);
            _playersInRange.Remove(playerObj);
        }
    }

    private void SpawnObjectForPlayer(IActorRef player) {
        var clientObj = Entity.GetClientBehaviorInstance();

        // Send object data to the player
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = _serializer.Serialize(clientObj)
        };
        player.Tell(newObjectMsg);
    }

    private void DespawnObjectForPlayer(IActorRef player) {
        // Send object data to the player
        var despawnObjectMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        };
        player.Tell(despawnObjectMsg);
    }

}