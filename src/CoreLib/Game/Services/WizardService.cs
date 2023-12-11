/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common.Caches;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

public class WizardService : MessageService {
    private Wizard _activeWizard;
    private TypeCache.CoreObject _activeWizardGameObject;

    public WizardService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new WizardService(parentActor));
    }

    protected override void OnDispose() {
        ((System.IDisposable) _activeWizard).Dispose();
        base.OnDispose();
    }

    #region Internal Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
    private void ReceiveZoneAddPlayerResponse(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message) {
        _activeWizardGameObject = message.WizardGameObject;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SETACTIVEWIZARD))]
    private void ReceiveSetActiveWizard(CHARACTER_103_PROTOCOL.MSG_SETACTIVEWIZARD message) {
        _activeWizard = message.Wizard;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD))]
    private void ReceiveQueryActiveWIzard(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD message) {
        Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_CHARACTER() {
            Wizard = _activeWizard,
            WizardGameObject = _activeWizardGameObject
        });
    }

    #endregion

    #region Game Handlers

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
    private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message) {
        // Save the player's location and direction on interval.
        // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
        // Yaw is represented in radians in the client, but transmitted to the server as degrees.
        var position = new SharpDX.Vector3(
            unchecked((short) message.LocationX * 4),
            unchecked((short) message.LocationY * 4),
            unchecked((short) message.LocationZ * 4));

        _activeWizard.SetCachedLocation(position);
        _activeWizard.SetCachedOrientation(message.Direction);
    }

    #endregion
}
