/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.Shared.Networking;

namespace Imlight.Server.Login.Services
{
    internal class CharacterService : MessageService
    {
        private uint _characterCreationStage;
        private uint _characterCreationParameter;

        public CharacterService(SessionActor parentActor) : base(parentActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new CharacterService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER))]
        private void ReceiveCreateCharacter(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER message)
        {
            // The client has sent us serialized WizardCharacterCreationData. We need to
            // deserialize it to add it to our account database.
            var serializer = new ObjectSerializer();
            var charData = (TypeCache.WizardCharacterCreationInfo)serializer.Deserialize(message.CreationInfo);

            var errorCode = 0;
            if (charData is null)
            {
                throw new ActorKilledException("Could not successfully deserialize WizardCharacterCreationData!");
            }

            // Add the new character to the player's account.
            var account = GetSocketAccount();
            if (account is not null)
            {
                var newCharacter = new Character(charData);
                var createdCharacter = account.AddCharacter(newCharacter);

                // @TODO: Figure out what each of these error codes means.
                if (createdCharacter is null)
                    errorCode = 2;
            }
            else
            {
                errorCode = 1;
            }

            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE()
            {
                ErrorCode = errorCode,
            });
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST))]
        private void ReceiveRequestCharacterList(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST message)
        {
            var account = GetSocketAccount();
            if (account is null)
            {
                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST() { Error = 1 });
                return;
            }

            // Tell the client we're going to start sending the character list.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());

            // For every character, we're going to serialize the document and send to the client.
            if (account.Characters.Count > 0)
            {
                var serializer = new ObjectSerializer();
                for (int i = 0; i < account.Characters.Count; i++)
                {
                    var character = account.Characters[i];

                    // WizApi saves the object. We need to serialize it here.
                    var data = serializer.Serialize(character.GetCharacterCreationInfo());
                    SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO() { CharacterInfo = data });
                }
            }

            // Tell the client we've finished sending the character list.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER))]
        private void ReceiveDeleteCharacter(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER message)
        {
            var errorCode = 0;
            var account = GetSocketAccount();
            if (account is null)
                errorCode = 1;
            
            if (account != null && !account.DeleteCharacter(message.CharID))
                errorCode = 2;

            SendToSocket(new LOGIN_7_PROTOCOL.MSG_DELETECHARACTERRESPONSE()
            {
                ErrorCode = errorCode
            });
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION))]
        private void ReceiveLoginLogCharacterCreation(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION message)
        {
            this._characterCreationParameter = message.Parameter;
            this._characterCreationStage = message.Stage;
        }
    }
}
