using Akka.Actor;
using Imlight.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;

namespace Imlight.Login.Services
{
    internal class CharacterServiceActor : MessageService
    {
        private ByteString _characterRaw;

        public CharacterServiceActor(SessionActor parentActor) : base(parentActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new CharacterServiceActor(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER))]
        private void ReceiveCreateCharacter(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER message)
        {
            _characterRaw = message.CreationInfo;

            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST))]
        private void ReceiveRequestCharacterList(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST message)
        {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());
            if (_characterRaw.Length != 0) {
                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO() { CharacterInfo = _characterRaw });
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
