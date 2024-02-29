/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;
internal class ShopService : MessageService {

    public ShopService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new InteractService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_DONESHOPPING))]
    private void ReceiveDoneShopping(WIZARD_12_PROTOCOL.MSG_DONESHOPPING message) {
        var wizard = GetActiveWizard();
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
            GameObjectID = wizard.GameObject.m_globalID,
            WizBangID = 0
        };
        ZoneBroadcast(wizBangMsg, false);
    }
}
