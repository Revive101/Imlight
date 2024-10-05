/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionVendor : ServiceOption {
    public override string ServiceName { get; protected set; } = "WizShoppingService";
    public override string WizBang { get; set; } = "Shopping";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_NPCInteractText";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new EquipmentShopOption() {
            m_displayKey = "GUI_ShopOptionEquipment",
            m_forceInteract = false,
            m_iconKey = "Shopping",
            m_serviceIndex = 0,
            m_serviceName = "WizShoppingService"
        }
    };

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private readonly List<GID> _inventory = new();

    public ServiceOptionVendor(CoreObject ActiveGameObject, List<GID> inventory) : base(ActiveGameObject) {
        _inventory = inventory;
        RecalculateOnProximityEnter = false;
    }

    public bool HasShopItem(GID item)
        => _inventory.Contains(item);

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
        var shopOffering = new WizShopOffering() {
            m_CSRTestShop = false,
            m_activeHolidayList = null,
            m_furnitureShop = 0,
            m_recipeList = null,
            m_sellModifier = 0.05f,
            m_shopTitle = "KrocNPC_00000013",
            m_shopList = _inventory,

            // Changes the type of currency that is used
            // 0 - Gold
            // 1 - PvP tickets
            m_shopType = 0,
        };
        var data = _serializer.Serialize(shopOffering);

        var shopListMsg = new WIZARD_12_PROTOCOL.MSG_SHOPLIST() {
            GlobalID = ActiveGameObject.m_globalID,
            Data = data,
            Credits = 0,
            WebFailure = 0,
        };
        suspect.Tell(shopListMsg);
    }

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect)
        => ServiceOptionBases;
}
