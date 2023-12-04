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

public class CharacterService : MessageService {
    private Wizard _activeCharacter;
    private TypeCache.CoreObject _activeCharacterObject;

    public CharacterService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new CharacterService(parentActor));
    }

    protected override void OnDispose() {
        ((System.IDisposable) _activeCharacter).Dispose();
        base.OnDispose();
    }

    #region Internal Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
    private void ReceiveZoneAddPlayerResponse(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message) {
        _activeCharacterObject = message.PlayerObject;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER))]
    private void ReceiveSetActiveCharacter(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER message) {
        _activeCharacter = message.Character;
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER))]
    private void ReceiveQueryActiveCharacter(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER message) {
        Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_CHARACTER() {
            Character = _activeCharacter,
            CharacterObject = _activeCharacterObject
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

        _activeCharacter.SetLocation(position);
        _activeCharacter.SetOrientation(message.Direction);
    }

    #endregion
}
