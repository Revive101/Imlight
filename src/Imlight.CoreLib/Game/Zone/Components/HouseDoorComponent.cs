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
 * HOUSE DOOR COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Makes a house door (an object with a TeleportProximityBehaviorTemplate) lead to the house on its other side when a
 * player walks up to it. The client has no proximity test for these doors, so the server detects it.
 *
 * USAGE EXAMPLE:
 * Attached to every entity whose template has a TeleportProximityBehaviorTemplate; nothing to call.
 *
 * NOTE:
 * - The component only notices the player and tells their session which door it was. The session knows which house the
 *   player is in, and it does everything that touches the player's items and the zone transfer.
 * - A door has no RenderBehaviorTemplate, so no RenderComponent sends it. The client only routes MSG_REQUESTHOUSINGZONE
 *   to an object it holds under the request's GlobalID, so this component sends the door itself with MSG_NEWOBJECT.
 * - A player who arrives inside the radius, as everyone does who walks through a door, has to walk out of it before
 *   the door works again.
 *
 * TODO:
 * - The offset of the behavior template is added to the door's position as it is. Is it relative to the door's yaw?
 *   Every house door in the game data has a zero offset.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class HouseDoorComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    // Keyed by the player's actor: a CoreObject is a record, so its hash changes with every move.
    private readonly HashSet<IActorRef> _playersInRange = [];
    private readonly CoreObjectSerializer _serializer = new(versionable: false, behaviors: SerializerFlags.None);
    private readonly PropertyFlags _propertyFlags = PropertyFlags.Prop_Public
                                                  | PropertyFlags.Prop_Transmit
                                                  | PropertyFlags.Prop_AuthorityTransmit;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate go && go.m_behaviors?.OfType<TeleportProximityBehaviorTemplate>().Any() == true;

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        SendDoorObject(playerActor);

        if (IsInRange(playerObj)) {
            _playersInRange.Add(playerActor);
        }
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) => _playersInRange.Remove(playerActor);

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!IsInRange(playerObj)) {
            _playersInRange.Remove(playerActor);

            return;
        }

        if (!_playersInRange.Add(playerActor)) {
            return;
        }

        Logger.Information("Character {CharId} walked up to the house door {DoorGid} (template {TemplateId}) and the client is asked through gid {RequestGid}.",
            Logger.Args(playerWizard?.CharId, Entity.ActiveGameObject.m_globalID.Full, Entity.ActiveGameObject.m_templateID.Full, Entity.ActiveGameObject.m_globalID.Full));

        playerActor.Tell(new ZONE_102_PROTOCOL.MSG_HOUSEDOOR {
            DoorGid = Entity.ActiveGameObject.m_globalID,
            LocationName = Door?.m_locationName.ToString(),
        });
    }

    private void SendDoorObject(IActorRef playerActor) {
        if (!_serializer.Serialize(Entity.GetClientObject(), _propertyFlags, out var serializedData)) {
            Logger.Error("Failed to serialize the house door {DoorGid}.", Logger.Args(Entity.ActiveGameObject.m_globalID.Full));

            return;
        }

        playerActor.Tell(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializedData });
    }

    private TeleportProximityBehaviorTemplate Door
        => (Entity.Template as GameObjectTemplate)?.m_behaviors?.OfType<TeleportProximityBehaviorTemplate>().FirstOrDefault();

    private bool IsInRange(CoreObject playerObj) {
        var door = Door;
        if (door is null || playerObj is null) {
            return false;
        }

        var center = Entity.ActiveGameObject.m_location + new Imcodec.Math.Vector3(door.m_offsetX, door.m_offsetY, door.m_offsetZ);
        var distance = playerObj.m_location - center;

        return distance.LengthSquared() <= door.m_radius * door.m_radius;
    }

}
