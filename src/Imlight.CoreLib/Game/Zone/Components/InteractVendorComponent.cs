/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractVendorComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "WizShoppingService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.Shopping;
    public string StateName       => "Shop"; // Forbids the player from moving.
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_ShopOptionEquipment";

    private List<GID> _inventory;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        // Attach if the template is an NPC and has an inventory in Dragon database,
        // or if the template is a vendor as per game client data.
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && (NpcInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out _)
        || WorldVendorLocations.IsVendor(goTemplate.m_templateID));

    public override void OnStart() {
        if (!NpcInventoryCollection.TryGetNpcInventory(Entity.ActiveGameObject.m_templateID, out var inventory)) {
            Logger.Error("Failed to get vendor inventory for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        _inventory = inventory.Inventory;
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new EquipmentShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        SendShopOfferings(playerActor);
        SendPlayerIntoWizbang(playerObject.m_globalID);
        SendPlayerIntoState(playerObject.m_globalID);
    }

    public bool HasItem(GID itemGID)
        => _inventory.Any(x => x.MParts.TemplateId == itemGID.MParts.TemplateId);

    private void SendShopOfferings(IActorRef playerActor) {
        var shopOffering = new WizShopOffering() {
            m_sellModifier = 0.05f,
            m_shopTitle = "KrocNPC_00000013",
            m_shopList = _inventory,

            // Changes the type of currency that is used
            // 0 - Gold
            // 1 - PvP tickets
            m_shopType = 0,

            // todo: figure this out for QA
            m_CSRTestShop = false,
        };

        // Serialize the offerings and send them to the player.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(shopOffering, 4, out var serializedShopList)) {
            Logger.Error("Failed to serialize shop offering for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        var shopListMsg = new WIZARD_12_PROTOCOL.MSG_SHOPLIST() {
            GlobalID = Entity.ActiveGameObject.m_globalID.Full,
            Data = serializedShopList
        };
        playerActor.Tell(shopListMsg);
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