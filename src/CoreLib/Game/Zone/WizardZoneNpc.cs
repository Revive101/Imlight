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
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a zone NPC which manages itself as an actor.
/// </summary>
public class WizardZoneNpc : WizardZoneObject {
    private static readonly string[] s_shopKeeperNameGiveaways = new string[] {
        "shop",
        "explorer",        // Zeke
        "crown_furniture"  // Eliose
    };

    // ctor
    public WizardZoneNpc(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) { }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneNpc(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        base.OnPlayerJoin(player, suspect);

        // Not sure if this can ever occur. Template for a ZoneObject should always be of this type.
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        // If the NPC is a shopkeeper, send a WIZBANG message to the client.
        var npcName = gameObjTemplate.m_objectName.ToString().ToLower();
        if (s_shopKeeperNameGiveaways.Any(npcName.Contains)) {
            var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
                WizBangID = StringHash.Compute("Shopping"),
                GameObjectID = ActiveGameObject.m_globalID
            };
            suspect.Tell(wizBangMsg);
        }

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

}
