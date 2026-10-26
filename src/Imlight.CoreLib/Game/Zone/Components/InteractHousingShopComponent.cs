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
 * INTERACT HOUSING SHOP
 * ========================================================================
 *
 * PURPOSE:
 * Opens the client's furniture shop window for an NPC whose shopping behavior names that window, with the NPC's
 * SpiralDB stock and the player's Crowns balance.
 *
 * USAGE EXAMPLE:
 * Attaches to an NPC template whose WizShoppingBehaviorTemplate has m_furnitureShop 1. ShopService handles the buy
 * request and issues a house through HouseDeeds.
 *
 * NOTE:
 * - The client picks the shop window from m_furnitureShop; 1 is the furniture window, where houses are listed.
 * - InteractVendorComponent does not attach to these NPCs, so a housing shop offers one shop option.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractHousingShopComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "WizShoppingService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.Shopping;
    public string StateName       => "Shop";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_ShopOptionHousing";

    private const int FURNITURE_SHOP_WINDOW_KIND = 1;
    private const float SHOP_SELL_MODIFIER = 0.05f;

    private List<GID> _inventory = [];
    private string _shopTitle = "";
    private int _shopType;

    /// <summary>
    /// Whether a template's shopping behavior opens the furniture window.
    /// </summary>
    /// <param name="template">The NPC template.</param>
    /// <returns><c>true</c> for a housing shop.</returns>
    public static bool IsHousingShop(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.OfType<WizShoppingBehaviorTemplate>()
            .Any(shopping => shopping.m_furnitureShop == FURNITURE_SHOP_WINDOW_KIND);

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(behavior => behavior is NPCBehaviorTemplate)
        && IsHousingShop(goTemplate)
        && NpcInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out _);

    public override void OnStart() {
        if (!NpcInventoryCollection.TryGetNpcInventory(Entity.ActiveGameObject.m_templateID, out var inventory)) {
            Logger.Error("Housing shop {TemplateId} has no stock.", Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        _inventory = inventory.Inventory;
        var shopping = Entity.Template?.m_behaviors?.OfType<WizShoppingBehaviorTemplate>().FirstOrDefault();
        _shopTitle = shopping?.m_shopTitle ?? "";
        _shopType = shopping?.m_shopType ?? 0;
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new HousingShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        SendShopOfferings(playerActor, playerCharacter.Account?.Crowns ?? 0);
        Broadcast(new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) WizBang,
            GameObjectID = playerObject.m_globalID
        });
        Broadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            State = StringHash.Compute(StateName),
            GameObjectID = playerObject.m_globalID
        });
    }

    /// <summary>
    /// Whether the shop sells an item.
    /// </summary>
    /// <param name="itemGID">The shop id the client sent.</param>
    /// <returns><c>true</c> when an item of the same template is in stock.</returns>
    public bool HasItem(GID itemGID)
        => _inventory.Any(x => x.MParts.TemplateId == itemGID.MParts.TemplateId);

    private void SendShopOfferings(IActorRef playerActor, int crowns) {
        var shopOffering = new WizShopOffering {
            m_sellModifier = SHOP_SELL_MODIFIER,
            m_shopTitle = _shopTitle,
            m_shopList = _inventory,
            m_shopType = _shopType,
            m_furnitureShop = FURNITURE_SHOP_WINDOW_KIND,
        };

        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        if (!serializer.Serialize(shopOffering, 4, out var serializedShopList)) {
            Logger.Error("Failed to serialize the offering of housing shop {TemplateId}.",
                Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_SHOPLIST {
            GlobalID = Entity.ActiveGameObject.m_globalID.Full,
            Data = serializedShopList,
            // The client shows this balance when the player pays with Crowns.
            Credits = crowns,
        });
    }

    private void Broadcast(IMessage message)
        => Entity.ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = message,
            Selfless = false,
        });

}
