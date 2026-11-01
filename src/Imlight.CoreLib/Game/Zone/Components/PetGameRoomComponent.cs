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
 * PET GAME ROOM COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Sits on the game object of a pet game zone. It sends that object to each player who joins the zone, tells
 * them which pet game the room runs, and introduces the players in the room to each other's sessions.
 *
 * USAGE EXAMPLE:
 * Attaches to objects whose template has a pet game behavior; the HatchingService takes it from there.
 *
 * NOTE:
 * The client builds the game's logic from this object, and only then answers MSG_PETGAMEINIT with READY. The
 * object has no render behavior, so RenderComponent never attaches to it and this component sends it instead.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
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

internal sealed class PetGameRoomComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    private const PropertyFlags OBJECT_PROPERTY_FLAGS = PropertyFlags.Prop_Public
                                                      | PropertyFlags.Prop_Transmit
                                                      | PropertyFlags.Prop_AuthorityTransmit;

    private readonly Dictionary<ulong, IActorRef> _players = [];
    private readonly CoreObjectSerializer _serializer = new(versionable: false, behaviors: SerializerFlags.None);

    private string GameName
        => (Entity.Template as GameObjectTemplate)?.m_behaviors?.OfType<PetGameBehaviorTemplate>().FirstOrDefault()?.m_gameName.ToString() ?? "";

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObject
            && gameObject.m_behaviors?.Any(behavior => behavior is PetGameBehaviorTemplate) == true;

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        SendObjectTo(playerActor);
        foreach (var other in _players.Values) {
            other.Tell(new ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERJOINED { CharId = playerWizard.CharId, Session = playerActor });
        }

        playerActor.Tell(new ZONE_102_PROTOCOL.MSG_PETGAMEROOMJOINED {
            GameName = GameName,
            OtherPlayers = new Dictionary<ulong, IActorRef>(_players),
        });
        _players[playerWizard.CharId] = playerActor;
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        var leaving = _players.FirstOrDefault(player => player.Value.Equals(playerActor));
        if (leaving.Value is null) {
            return;
        }

        _players.Remove(leaving.Key);
        foreach (var other in _players.Values) {
            other.Tell(new ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERLEFT { CharId = leaving.Key });
        }
    }

    private void SendObjectTo(IActorRef playerActor) {
        if (!_serializer.Serialize(Entity.GetClientObject(), OBJECT_PROPERTY_FLAGS, out var data)) {
            Logger.Error("Could not serialize the pet game object of {Game}.", Logger.Args(GameName));

            return;
        }

        playerActor.Tell(new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = data });
    }

}
