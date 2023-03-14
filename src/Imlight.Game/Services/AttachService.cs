using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using Imlight.Data;
using Imlight.Net.Messages;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Game.Services
{
    internal class AttachService : MessageService
    {
        public AttachService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new AttachService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ATTACH))]
        private void ReceiveAttach(GAME_5_PROTOCOL.MSG_ATTACH message)
        {
            // Use the session key given in the message to ensure that the user didn't bypass our login server.
            if (!ValidateLoginKey(message.LoginKey, message.UserID, out var account))
            {
                Log.Logger.Warning($"User [{message.UserID}] failed to validate login key: {message.LoginKey}.");

                var attachFailedMsg = new GAME_5_PROTOCOL.MSG_ATTACHFAILED()
                {
                    Error = 1,
                    Rejected = 1,
                };
                SendToSocket(attachFailedMsg);
                
                return;
            }
            
            // This is the first authentication action the user will send on the game server. Using the session key
            // given, we'll set the AccountService account to what the key is mapped to.
            SetAccountInternally(account);

            if (!GetCharacter(message.CharID, out var character))
            {
                Log.Logger.Error($"Could not get character by ID on MSG_ATTACH!");

                SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED()
                {
                    Error = 1,
                    NoDisconnect = 1, // @todo: find out what these error codes mean.
                    Rejected = 1,
                });

                return;
            }
            
            var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE()
            {
                ZoneName = message.ZoneName,
                ZoneID = message.ZoneID,
                Data = GetCharacterData(character),
                DynamicZoneID = 0,
                DynamicServerProcID = 0,
                IsCSR = 1,
                Permissions = 31679,
                RealmName = "Imlight",
                //ZoneID = new GID(4288020480),
                //CriticalObjects = null,
            };

            SendToSocket(loginCompleteMsg);
        }

        private ByteString GetCharacterData(Character character)
        {
            // =============================================================
            // THIS IS ENTIRELY DEBUG ONLY AND MUST BE REMOVED LATER
            // =============================================================
            var serializer = new CoreObjectSerializer();
            var charClientObject = character.GetWizClientObject();
            var charData = serializer.SerializeCoreObject(charClientObject);

            return charData;
        }

        private bool GetCharacter(ulong charId, out Character character)
        {
            var account = Data.Util.GetDebugAccount();

            var result = account.GetCharacter(charId, out var accChar);
            character = accChar;

            return result;
        }

        private bool ValidateLoginKey(ByteString key, ulong userId, out Account account)
        {
            account = null;
            
            var msg = new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY()
            {
                Key = key,
                UserID = userId
            };
            var rsp = AskServer<SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP>(msg);

            account = rsp.Account;
            return rsp.ErrorCode == 0;
        }

        private void SetAccountInternally(Account account)
        {
            // Tell the SessionActor to set the account.
            SendInternal(new ACCOUNT_104_PROTOCOL.INTMSG_SET_ACCOUNT()
            {
                Account = account
            });
        }
    }
}
