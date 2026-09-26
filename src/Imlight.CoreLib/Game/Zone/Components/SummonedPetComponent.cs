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
 * SUMMONED PET COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Removes a summoned pet from the world when its owner unequips it or leaves the zone,
 * for the owner and every other player in the zone.
 *
 * USAGE EXAMPLE:
 * Attaches to every PetObject entity. EquipmentService sends MSG_DISMISSPET with the pet's
 * world GID; the zone's MSG_REMOVEPLAYER for the owner dismisses it as well.
 *
 * NOTE:
 * The owner is the LeashBehavior's m_ownerGid, the same player object GID the zone
 * reports when that player leaves.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Pet;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class SummonedPetComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    private bool _dismissed;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate { m_templateID: PetFactory.GENERIC_PET_TEMPLATE_ID };

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        if (id != 0 && id == GetOwnerId()) {
            Dismiss();
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_DISMISSPET))]
    private void ReceiveDismissPet(ZONE_102_PROTOCOL.MSG_DISMISSPET message) {
        if (message.PetGlobalId == Entity.ActiveGameObject.m_globalID) {
            Dismiss();
        }
    }

    private ulong GetOwnerId()
        => Entity.ActiveGameObject.m_inactiveBehaviors?.OfType<LeashBehavior>().FirstOrDefault()?.m_ownerGid.Full ?? 0;

    private void Dismiss() {
        if (_dismissed) {
            return;
        }

        _dismissed = true;

        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        });
        ZoneActor.Tell(new ZONE_102_PROTOCOL.MSG_RELEASEMOBILEID {
            MobileId = Entity.MobileID
        });

        // The entity actor is this component's parent; stopping it stops every component with it.
        Context.Parent.Tell(PoisonPill.Instance);
    }

}
