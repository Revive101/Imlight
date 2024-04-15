/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone NPC which manages itself as an actor.
/// </summary>
public class WizardZoneNpc : WizardZoneObject {
    private static readonly string[] s_dyeShopNameGiveaways = new string[] {
        "dye",
    };
    private static readonly string s_auctionHouseName = "kt-hub-npc14";

    public bool IsShopkeeper { get; set; }
    public ServiceMementoBase ServiceMomentoBase { get; private set; }
    public List<GID> Inventory { get; set; } = new();

    private readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    private readonly string _npcNameKey = "NPCFormats_Name";
    private MadlibBlock _madlibBlock;
    private bool _turnTowardsPlayer;

    // ctor
    public WizardZoneNpc(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        SetMadLibBlock();
        SetServiceMomentoBase();

        // Check to see if we're a shopkeeper. If we are, set the shopkeeper properties.
        // For some reason, dye shops are not included in the world vendor locations.
        var npcName = gameObjTemplate.m_objectName.ToString().ToLower();
        if (s_dyeShopNameGiveaways.Any(npcName.Contains)) {
            SetDyeShop();
        }
        else if (npcName == s_auctionHouseName) {
            SetAuctionHouse();
        }
        else if (WorldVendorLocations.IsVendor(gameObjTemplate.m_templateID)) {
            SetShopkeeper();
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneNpc(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        base.OnPlayerJoin(player, suspect);

        if (IsShopkeeper) {
            var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
                WizBangID = StringHash.Compute("Shopping"),
                GameObjectID = ActiveGameObject.m_globalID
            };
            suspect.Tell(wizBangMsg);
        }

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        if (IsShopkeeper) {
            var data = _serializer.Serialize(ServiceMomentoBase);

            var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
                MobileID = ActiveGameObject.m_globalID,
                Options = data,
                Reinteract = 0
            };

            suspect.Tell(npcOptionsMsg);
        }
    }

    protected override void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionExit(player, suspect);

        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = ActiveGameObject.m_globalID
        };
        suspect.Tell(leaveServiceRangeMsg);
    }

    private void SetMadLibBlock() {
        var gameObjTemplate = Template as GameObjectTemplate;
        if (gameObjTemplate is null) {
            return;
        }

        var madlibList = new List<MadlibArg> {
            new MadlibArgT_std_string() {
                m_madlibArgument = gameObjTemplate.m_displayName,
                m_madlibToken = "NAME"
            },
        };

        _madlibBlock = new MadlibBlock() {
            m_blockToken = "NPC",
            m_madlibs = madlibList
        };
    }

    private void SetServiceMomentoBase() {
        var gameObjTemplate = Template as GameObjectTemplate;

        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = _turnTowardsPlayer,
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = gameObjTemplate.m_sIcon,
            m_npcNameKey = _npcNameKey,
            m_npcTextKey = "GUI_NPCInteractText",
            m_personaMadlibs = _madlibBlock,
            m_serviceOptions = new List<ServiceOptionBase>()
        };
    }

    private void SetShopkeeper() {
        IsShopkeeper = true;
        var gameObjTemplate = Template as GameObjectTemplate;

        // Get inventory from WorldDatabase
        var getInventorySuccess = NpcInventoryCollection.TryGetNpcInventory(ActiveGameObject.m_templateID, out var npcInventory);
        if (!getInventorySuccess) {
            Inventory = new List<GID>() { new GID(1363076) }; // Default to selling One Ring
            return;
        }

        Inventory = npcInventory.Inventory;

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

    private void SetDyeShop() {
        IsShopkeeper = true;
        var gameObjTemplate = Template as GameObjectTemplate;

        // What a funny line, C# pattern matching.
        if (Template.m_behaviors.FirstOrDefault(x => x is NPCBehaviorTemplate) is NPCBehaviorTemplate npcBehavior) {
            _turnTowardsPlayer = npcBehavior.m_turnTowardsPlayer;
        }
        else {
            Logger.Error("NPC {0} is a shopkeeper but has no NPCBehaviorTemplate", Logger.Args(ActiveGameObject.m_debugName));
        }

        var dyeService = new DyeShopOption() {
            m_displayKey = "GUI_DyeShop",
            m_forceInteract = false,
            m_iconKey = "DyeShop",
            m_serviceIndex = 0,
            m_serviceName = "DyeShopService"
        };
        ServiceMomentoBase.m_serviceOptions.Add(dyeService);
    }

    private void SetAuctionHouse() {
        IsShopkeeper = true;
        var gameObjTemplate = Template as GameObjectTemplate;

        if (Template.m_behaviors.FirstOrDefault(x => x is NPCBehaviorTemplate) is NPCBehaviorTemplate npcBehavior) {
            _turnTowardsPlayer = npcBehavior.m_turnTowardsPlayer;
        }
        else {
            Logger.Error("NPC {0} is a shopkeeper but has no NPCBehaviorTemplate", Logger.Args(ActiveGameObject.m_debugName));
        }

        var auctionHouseService = new AuctionHouseOption() {
            m_auctionHousePurchaseKey = 1, // Todo: Find out what this is
            m_displayKey = "GUI_AuctionHouse",
            m_forceInteract = false,
            m_iconKey = "Shopping",
            m_serviceIndex = 0,
            m_serviceName = "AuctionHouseService"
        };
        ServiceMomentoBase.m_serviceOptions.Add(auctionHouseService);
    }
}
