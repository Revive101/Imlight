using System;
using Akka.Actor;
using Imlight.Data;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.Cache;

namespace Imlight.Game.Services
{
    public class CharacterService : MessageService
    {
        private Character _activeCharacter;
        private TypeCache.CoreObject _activeCharacterObject;
        
        public CharacterService(SessionActor sessionActor) : base(sessionActor)
        {
        }
        
        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new CharacterService(parentActor));
        }

        [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER))]
        private void ReceiveSetActiveCharacter(CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER message)
        {
            _activeCharacter = message.Character;
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
        private void ReceiveZoneAddPlayerResponse(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message)
        {
            _activeCharacterObject = message.PlayerObject;
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

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
        private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            if (_activeCharacterObject is null) throw new NullReferenceException(nameof(_activeCharacterObject));
            
            // Normalize differentiating message values
            var x = unchecked((short)message.LocationX) * 4.0f;
            var y = unchecked((short)message.LocationY) * 4.0f;
            var z = unchecked((short)message.LocationZ) * 4.0f;
            var direction = (float)(message.Direction * Math.PI * 2 / 250);
            
            _activeCharacterObject.m_location = new SharpDX.Vector3(x, y, z);
        }
    }
}