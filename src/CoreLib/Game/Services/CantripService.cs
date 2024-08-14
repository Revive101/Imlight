/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
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
            // wtf is OutOfEnergy?
        };
        SendToSocket(cantripResponse);

        // TODO: look into dealing with multiple of these things
        var animationKFM = cantrip.m_animationKFMs.Count == 0 ? (ByteString) "" : cantrip.m_animationKFMs[0];
        var animationName = cantrip.m_animationNames.Count == 0 ? (ByteString) "" : cantrip.m_animationNames[0]; 

        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = GetActiveWizard().GameObject.m_globalID,
            SpellTemplateID = (int) message.SpellTemplateID,
            AnimationKFM = animationKFM,
            AnimationName = animationName
        };
        SendToSocket(castEffect);
    }
}