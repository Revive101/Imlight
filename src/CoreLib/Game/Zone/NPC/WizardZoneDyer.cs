/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.NPC;

/// <summary>
/// This is a zone NPC dye shop vendor which manages itself as an actor. It
/// derives from <see cref="WizardZoneNpc"/>.
/// </summary>
internal class WizardZoneDyer : WizardZoneNpc {

    // ctor
    public WizardZoneDyer(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
           : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        SetMadLibBlock();
        SetServiceMomentoBase();

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

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneDyer(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        base.OnPlayerJoin(player, suspect, wizard);

        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) WizBangs.Shopping,
            GameObjectID = ActiveGameObject.m_globalID
        };
        suspect.Tell(wizBangMsg);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerProximityEnter(CoreObject player, IActorRef suspect) {
        var data = _serializer.Serialize(ServiceMomentoBase);

        var npcOptionsMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDNPCOPTIONS {
            MobileID = ActiveGameObject.m_globalID,
            Options = data,
            Reinteract = 0
        };

        suspect.Tell(npcOptionsMsg);
    }
}
