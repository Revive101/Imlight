/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections;
using System.Net;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class CantripService : MessageService {

    public CantripService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CantripService(parentActor));

    protected override void OnDispose() {

    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST))]
    private void ReceiveCantripSpellCast(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST message) {
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(message.SpellTemplateID);
        var cantripResponse = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSRESPONSE {
            EnergyUsed = (uint) cantrip.m_energyCost,
            CooldownSeconds = (uint) cantrip.m_cooldownSeconds
            // if user is out of energy, set OutOfEnergy to 1
        };
        SendToSocket(cantripResponse); 

        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = GetActiveWizard().GameObject.m_globalID,
            SpellTemplateID = (int) message.SpellTemplateID
        };

        switch (cantrip.m_cantripsSpellEffect) {
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_Emote:
                castEffect.AnimationName = cantrip.m_animationNames[0]; break;
            case CantripsSpellTemplate.CantripsSpellEffect.CSE_PlayEffect:
                // note that the only cantrip from this section that needs rng is the dice roll one
                // all the other cantrips in this section only have 1 animation
                var rand = new Random();
                int num = rand.Next(cantrip.m_animationKFMs.Count);
                castEffect.AnimationKFM = cantrip.m_animationKFMs[num];
                castEffect.AnimationName = cantrip.m_animationNames[num];
                break;
        }
        SendToSocket(castEffect);
    }
}