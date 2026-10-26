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
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractAuctionHouseComponent(ZoneEntity entity) 
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "AuctionHouseService";
    public string NpcIcon => null;
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public WizBangs WizBang => WizBangs.Shopping;
    public string StateName => "Shop";
    public string InteractWizBang => "Registrar";
    public string DisplayKey => "GUI_AuctionHouse";

    private static readonly string s_auctionHouseName = "kt-hub-npc14";

    public static bool ShouldAttachToEntity(CoreTemplate template)
        // Attach if the template is an NPC and has an inventory in Dragon database,
        // or if the template is a vendor as per game client data.
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && goTemplate.m_objectName.ToString().ToLowerInvariant() == s_auctionHouseName;

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new AuctionHouseOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
                m_sellModifier = 1.0f,
                m_auctionHousePurchaseKey = 1
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        BroadcastPlayerEnteringService(playerObject.m_globalID, (uint) WizBang, StateName);
    }

}
