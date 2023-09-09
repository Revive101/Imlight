/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Server.Game.Models;
using SharpDX;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Services
{
    public class CharacterService : MessageService
    {
        private Character _activeCharacter;
        private TypeCache.CoreObject _activeCharacterObject;
        
        public CharacterService(SessionActor sessionActor) : base(sessionActor) { }
        
        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new CharacterService(parentActor));
        }

        protected override void OnDispose()
        {
            _activeCharacter.Dispose();
            base.OnDispose();
        }

        #region Internal Handlers
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
        private void ReceiveZoneAddPlayerResponse(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message)
        {
            _activeCharacterObject = message.PlayerObject;
        }
        
        [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER))]
        private void ReceiveSetActiveCharacter(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER message)
        {
            _activeCharacter = message.Character;
        }

        [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER))]
        private void ReceiveQueryActiveCharacter(CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER message)
        {
            Sender.Tell(new CHARACTER_103_PROTOCOL.MSG_CHARACTER()
            {
                Character = _activeCharacter,
                CharacterObject = _activeCharacterObject
            });
        }
        
        #endregion
        
        #region Game Handlers

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
        private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            // Save the player's location and direction on interval.
            
            if (_activeCharacterObject is null)
                throw new ServiceRetryException($"Tried to do client move but could not grab active character " +
                                                $"object");

            // Normalize differentiating message values
            var x = unchecked((short)message.LocationX) * 4.0f;
            var y = unchecked((short)message.LocationY) * 4.0f;
            var z = unchecked((short)message.LocationZ) * 4.0f;
            var direction = (float)(message.Direction * System.Math.PI * 2 / 250);
            
            _activeCharacterObject.m_location = new Vector3(x, y, z);
            _activeCharacterObject.m_orientation = new Vector3(0, 0, direction);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
        private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message)
        {
            if (_activeCharacterObject is null)
                throw new ServiceRetryException($"Tried to do client move but could not grab active character " +
                                                $"object");
        }

        // Experimental - Sets the Crowns to 12,345,678
        [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_CROWNBALANCE))]
        private void ReceiveCrownBalance(WIZARD_12_PROTOCOL.MSG_CROWNBALANCE message)
        {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE()
            {
                CharacterID = message.CharacterID,
                Failure = 0,
                TotalCrowns = 12345678,
                CacheBalanceForCSSegmentation = 1
            });
        }

        // Experimental - ??? should be something with stats
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_GETLADDER))]
        private void ReceiveGetLadder(GAME_5_PROTOCOL.MSG_GETLADDER message)
        {
            SendToSocket(new GAME_5_PROTOCOL.MSG_GETLADDER()
            {
                CharacterID = message.CharacterID,
                NameBlob = message.NameBlob,
                TournamentNameID = message.TournamentNameID,
            });
        }
        
        #endregion
    }
}