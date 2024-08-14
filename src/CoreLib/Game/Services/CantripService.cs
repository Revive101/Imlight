/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class CantripService : MessageService {

    private Wizard _activeWizard;
    private TypeCache.CoreObject _activeWizardGameObject;

    public CantripService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CantripService(parentActor));

    protected override void OnDispose() {
        ((System.IDisposable) _activeWizard)?.Dispose();
        base.OnDispose();
    }

    [MessageHandler(typeof(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST))]
    private void ReceiveCantripSpellCast(CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSSPELLCAST message) {
        CantripsSpellTemplate cantrip = CantripFactory.CreateCantripTemplateFromId(message.SpellTemplateID);
        var msg = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CANTRIPSRESPONSE {
            EnergyUsed = (uint) cantrip.m_energyCost,
            CooldownSeconds = (uint) cantrip.m_cooldownSeconds
            // wtf is OutOfEnergy?
        };
        SendToSocket(msg);
    }
}