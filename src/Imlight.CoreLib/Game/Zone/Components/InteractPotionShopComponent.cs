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
 * INTERACT POTION SHOP
 * ========================================================================
 * 
 * PURPOSE:
 * Makes potion-vendor NPCs (Hilda Brewer in the Wizard City Commons,
 * object WC_PotionShopkeeper) interactable. Clicking the NPC opens the
 * client's native Mystical Mixtures window via MSG_POTIONSHOPOPEN, where
 * the player chooses Fill One / Fill All.
 * 
 * USAGE EXAMPLE:
 * Same IServiceComponent + IComponentFactory shape as InteractDyeShopComponent,
 * auto-registered by reflection. The purchase itself is handled server-side
 * by PotionService.ReceivePotionBuyRequest.
 * 
 * NOTE:
 * Matches the "PotionShopkeeper" object-name family so any world's potion
 * shopkeeper becomes a working vendor.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 08/13/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractPotionShopComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string POTION_SHOP_NPC_CONTAINS = "PotionShopkeeper";
    private const string POTION_SHOP_TITLE = "WC-NPCs_00000867";

    public string ServiceName     => "PotionShopService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.Shopping;
    public string StateName       => "Shop";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_PotionShop";

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_objectName is not null // Some templates carry a null object name.
        && gameObjectTemplate.m_objectName.ToString().Contains(POTION_SHOP_NPC_CONTAINS);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new PotionShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        SendPlayerPotionShopOpen(playerActor, Entity.ActiveGameObject.m_globalID);
        SendPlayerIntoWizbang(playerObject.m_globalID);
        SendPlayerIntoState(playerObject.m_globalID);
    }

    private void SendPlayerPotionShopOpen(IActorRef playerActor, ulong objId) {
        var potionShopOpen = new WIZARD_12_PROTOCOL.MSG_POTIONSHOPOPEN() {
            GlobalID = objId,
            ShopTitle = POTION_SHOP_TITLE
        };

        playerActor.Tell(potionShopOpen);
    }

    private void SendPlayerIntoWizbang(ulong playerObjID) {
        // Create the wiz bang message, and wrap it in a broadcast message.
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) WizBang,
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = wizBangMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

    private void SendPlayerIntoState(ulong playerObjID) {
        // Create the change state message, and wrap it in a broadcast message.
        var changeStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            State = StringHash.Compute(StateName),
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = changeStateMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

}
