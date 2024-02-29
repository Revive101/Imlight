/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using System.Linq;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone NPC which manages itself as an actor.
/// </summary>
public class WizardZoneNpc : WizardZoneObject {
    private static readonly string[] s_shopKeeperNameGiveaways = new string[] {
        "shop",
    };
    private static readonly string[] s_explorerNames = new string[] {
        "prospector zeke",
        "eloise merryweather",
        "elik silverfist",
    };
    private readonly bool _areWeShopkeeper;

    // ctor
    public WizardZoneNpc(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var npcName = gameObjTemplate.m_objectName.ToString().ToLower();
        var debugName = ActiveGameObject.m_debugName.ToString().ToLower();
        if (s_shopKeeperNameGiveaways.Any(npcName.Contains) || s_explorerNames.Any(n => debugName == n)) {
            _areWeShopkeeper = true;
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneNpc(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        base.OnPlayerJoin(player, suspect);

        if (_areWeShopkeeper) {
            var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
                WizBangID = StringHash.Compute("Shopping"),
                GameObjectID = ActiveGameObject.m_globalID
            };
            suspect.Tell(wizBangMsg);
        }

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionEnter(player, suspect);

        var serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);

        if (_areWeShopkeeper) {
            var madlibList = new List<MadlibArg> {
                new MadlibArgT_std_string() {
                    m_madlibArgument = "Persona,First_00000039",
                    m_madlibToken = "FIRSTNAME"
                },
                new MadlibArgT_std_string() {
                    m_madlibArgument = "Persona,Last_00000033",
                    m_madlibToken = "LASTNAME"
                },
                new MadlibArgT_std_string() {
                    m_madlibArgument = "Title_00000018",
                    m_madlibToken = "TITLE"
                },
                new MadlibArgT_std_string() {
                    m_madlibArgument = "",
                    m_madlibToken = "NICKNAME"
                },
                new MadlibArgT_std_string() {
                    m_madlibArgument = "NPCFormats_First_Last",
                    m_madlibToken = "FULLNAME"
                },
            };

            var serviceOptions = new List<ServiceOptionBase> {
                new EquipmentShopOption() {
                    m_displayKey = "GUI_ShopOptionEquipment",
                    m_forceInteract = false,
                    m_iconKey = "Shopping",
                    m_serviceIndex = 0,
                    m_serviceName = "WizShoppingService"
                }
            };

            var serviceMementoBase = new ServiceMementoBase() {
                m_bTurnPlayerToFace = true,
                m_clickToInteractOnly = false,
                m_npcFarewellSound = "",
                m_npcGreetingSound = "",
                m_npcIcon = "GUI/NpcPortraits/Art_Portrait_Unknown.dds",
                m_npcNameKey = "NPCFormats_First_Last",
                m_npcTextKey = "GUI_NPCInteractText",
                m_personaMadlibs = new MadlibBlock() {
                    m_blockToken = "NPC",
                    m_madlibs = madlibList
                },
                m_serviceOptions = serviceOptions
            };

            var data = serializer.Serialize(serviceMementoBase);

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
}
