using Akka.Actor;
using Imlight.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Login.Services
{
    internal class CharacterService : MessageService
    {
        private ByteString _localChar;
        private TypeCache.WizardCharacterCreationInfo _info;

        public CharacterService(SessionActor parentActor) : base(parentActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new CharacterService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER))]
        private void ReceiveCreateCharacter(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER message)
        {
            _localChar = message.CreationInfo;

            // deserialization test
            var deserializer = new ObjectSerializer();
            _info = (TypeCache.WizardCharacterCreationInfo)deserializer.Deserialize(message.CreationInfo);

            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST))]
        private void ReceiveRequestCharacterList(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST message)
        {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());

            if (_localChar.Length != 0)
            {
                // Serialization test
                var serializer = new ObjectSerializer();
                var data = serializer.Serialize(_info);

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO()
                {
                    CharacterInfo = data
                });
            }
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK))]
        private void ReceiveLoginNotAfk(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message)
        {
            // @TODO
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION))]
        private void ReceiveLoginLogCharacterCreation(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION message)
        {
            // @TODO
        }
    }
}
