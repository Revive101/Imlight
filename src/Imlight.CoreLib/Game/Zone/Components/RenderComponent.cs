/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class RenderComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    ////////////////////////////////////////////////////////////////////////////
    /// There are two distinct messages to send to the client in regards to  ///
    /// objects in the game. The first is MSG_NEWOBJECT, which is used to    ///
    /// create a new object in the client's world.                           ///
    /// The second is MSG_ADDOBJECT, which respawns an object that was       ///
    /// previously removed. You cannot send MSG_ADDOBJECT in regards to an   ///
    /// object if the client has not been told about it with MSG_NEWOBJECT   ///
    ////////////////////////////////////////////////////////////////////////////

    private readonly CoreObjectSerializer _serializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );
    private readonly PropertyFlags _propertyFlags = PropertyFlags.Prop_Public
                                                  | PropertyFlags.Prop_Transmit
                                                  | PropertyFlags.Prop_AuthorityTransmit;
    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private float _renderDistance;
    private bool _doesDistanceCheck = false;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is RenderBehaviorTemplate)
        || template is CombatSigilTemplate; // bug fix: combat sigil templtaes don't have any behaviors

    public override void OnStart() {
        // Check if the object should be spawned based on distance.
        _doesDistanceCheck = Entity.Template.m_behaviors
                .OfType<AnimationBehaviorTemplate>()
                .Any(anim => anim.m_bFadesIn || anim.m_bFadesOut);

        _renderDistance = Entity.Zone.ZoneData.m_farClip;

        CreateObjectForAllPlayers();
    }

    public override void OnEnabled() 
        => CreateObjectForAllPlayers();

    public override void OnDisabled() =>
        // Broadcast the removal of the object to all players.
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        });

    public override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        CreateObjectForPlayer(suspect);

        // If the player is not within our render distance, despawn.
        if (!_doesDistanceCheck) {
            return;
        }

        if (!IsInRadius(player, _renderDistance)) {
            DespawnObjectForPlayer(suspect);
        }
        else {
            _playersInRange.Remove(player);
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

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!_doesDistanceCheck) {
            return;
        }

        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, _renderDistance) && !_playersInRange.ContainsKey(playerObj)) {
            // If the player is in range, spawn the object for them.
            RespawnObjectForPlayer(playerActor);
            _playersInRange.Add(playerObj, playerActor);
        }
        else if (!IsInRadius(playerObj, _renderDistance) && _playersInRange.ContainsKey(playerObj)) {
            // If the player is out of range, despawn the object for them.
            DespawnObjectForPlayer(playerActor);
            _playersInRange.Remove(playerObj);
        }
    }

    private void CreateObjectForPlayer(IActorRef player) {
        // Serialize the client object.
        var clientObj = Entity.GetClientBehaviorInstance();
        if (!_serializer.Serialize(clientObj, _propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize object data.");

            return;
        }

        // Send object data to the player.
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = serializedData
        };
        player.Tell(newObjectMsg);
    }

    private void CreateObjectForAllPlayers() {
        // Serialize the client object.
        var clientObj = Entity.GetClientBehaviorInstance();
        if (!_serializer.Serialize(clientObj, _propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize object data.");

            return;
        }

        // Send object data to all players.
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = serializedData
        });
    }

    private void RespawnObjectForPlayer(IActorRef player) {
        // Send object data to the player.
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_ADDOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID,
            LocationX = Entity.ActiveGameObject.m_location.X,
            LocationY = Entity.ActiveGameObject.m_location.Y,
            LocationZ = Entity.ActiveGameObject.m_location.Z,
            Direction = Entity.ActiveGameObject.m_orientation.Z
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