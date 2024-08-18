/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.NPC;

/// <summary>
/// This is a zone NPC item vendor which manages itself as an actor. It
/// derives from <see cref="WizardZoneNpc"/>.
/// </summary>
internal class WizardZoneVendor : WizardZoneNpc {

    public List<GID> Inventory { get; set; } = new();

    // ctor
    public WizardZoneVendor(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
            : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        SetMadLibBlock();
        SetServiceMomentoBase();

        // Get inventory from WorldDatabase
        var getInventorySuccess = NpcInventoryCollection.TryGetNpcInventory(ActiveGameObject.m_templateID, out var npcInventory);
        if (!getInventorySuccess) {
            Inventory = new List<GID>() { new GID(1363076) }; // Default to selling One Ring
        }
        else {
            Inventory = npcInventory.Inventory;
        }

        // What a funny line, C# pattern matching.
        if (Template.m_behaviors.FirstOrDefault(x => x is NPCBehaviorTemplate) is NPCBehaviorTemplate npcBehavior) {
            _turnTowardsPlayer = npcBehavior.m_turnTowardsPlayer;
        }
        else {
            Logger.Error("NPC {0} is a shopkeeper but has no NPCBehaviorTemplate", Logger.Args(ActiveGameObject.m_debugName));
        }

        var shopService = new EquipmentShopOption() {
            m_displayKey = "GUI_ShopOptionEquipment",
            m_forceInteract = false,
            m_iconKey = "Shopping",
            m_serviceIndex = 0,
            m_serviceName = "WizShoppingService"
        };
        ServiceMomentoBase.m_serviceOptions.Add(shopService);
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneVendor(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        base.OnPlayerJoin(player, suspect, wizard);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) WizBangs.Shopping,
            GameObjectID = ActiveGameObject.m_globalID
        };
        suspect.Tell(wizBangMsg);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        var data = _serializer.Serialize(ServiceMomentoBase);

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = ActiveGameObject.m_globalID,
            Options = data,
            Reinteract = 0
        };

        suspect.Tell(npcOptionsMsg);
    }

    protected override void ReceiveNpcInteract(QUEST_MESSAGES_52_PROTOCOL.MSG_INTERACTNPC message) {
        var shopOffering = new WizShopOffering() {
            m_CSRTestShop = false,
            m_activeHolidayList = null,
            m_furnitureShop = 0,
            m_recipeList = null,
            m_sellModifier = 0.05f,
            m_shopTitle = "KrocNPC_00000013",
            m_shopList = Inventory,

            // Changes the type of currency that is used
            // 0 - Gold
            // 1 - PvP tickets
            m_shopType = 0,
        };
        var data = _serializer.Serialize(shopOffering);

        var shopListMsg = new WIZARD_12_PROTOCOL.MSG_SHOPLIST() {
            GlobalID = message.GlobalID,
            Data = data,
            Credits = 0,
            WebFailure = 0,
        };
        Sender.Tell(shopListMsg);
    }
}
