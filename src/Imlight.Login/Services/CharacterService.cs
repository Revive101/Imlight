using Akka.Actor;
using Imlight.Common;
using Imlight.Data;
using Imlight.Net;
using Imlight.Net.Messages;
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

            int errorCode = 0;
            if (charData is null)
            {
                Log.Logger.Error("Could not successfully deserialize WizardCharacterCreationData!");
                errorCode = 1;
            }

            // Add the new character to the player's account.
            var account = GetSocketAccount();
            if (account is not null && charData is not null)
            {
                var newCharacter = new Character(charData);
                newCharacter.CreationData.m_userID = (GID)account.ID;

                var result = account.AddCharacter(newCharacter);

                // @TODO: Figure out what each of these error codes means.
                if (result == false)
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
                return;

            // Tell the client we're going to start sending the character list.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());

            // For every character, we're going to serialize the document and send to the client.
            if (account.Characters.Count > 0)
            {
                var serializer = new ObjectSerializer();
                for (int i = 0; i < account.Characters.Count; i++)
                {
                    var character = account.Characters[i];

                    // Remember, WizAPI saves the object. We need to serialize it here.
                    var data = serializer.Serialize(character.CreationData);
                    SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO()
                    {
                        CharacterInfo = data,
                    });
                }
            }

            // Tell the client we've finished sending the character list.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER))]
        private void ReceiveDeleteCharacter(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER message)
        {
            int errorCode = 0;
            var account = GetSocketAccount();
            if (account is null)
                errorCode = 1;

            // The DeleteCharacter method will do the character searching for us.
            // Returns false if no character by that ID is found.
            if (!account.DeleteCharacter(message.CharID))
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

        private Account GetSocketAccount()
        {
            // Get the account from the AccountService.
            var internalMessage = new INTERN_ACCOUNT_PROTOCOL.INTMSG_GET_ACCOUNT();
            var account = AskInternal<INTERN_ACCOUNT_PROTOCOL.INTMSG_ACCOUNT>(internalMessage).Account;
            
            if (account is null)
            {
                Log.Logger.Error($"{this.GetType()} could not get account from AccountService.");
            }

            return account;
        }
    }
}
