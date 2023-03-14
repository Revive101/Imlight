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
            Log.Logger.Debug($"Attach received key {message.LoginKey}");
            
            if (!ValidateLoginKey(message.LoginKey))
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

            var data = GetCharacterData(character);

            var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE()
            {
                Data = data,
                ZoneName = character.CreationData.m_location,
                DynamicZoneID = 4288020480,
                DynamicServerProcID = 57781,
                IsCSR = 1,
                Permissions = 31679,
                RealmName = "Imlight",
                ZoneID = new GID(4288020480),
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

        private bool ValidateLoginKey(ByteString key)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY()
            {
                Key = key,
                SessionID = SessionActor.SessionID
            };

            var rsp = AskServer<SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP>(msg);
            
            return rsp.ErrorCode == 0;
        }
    }
}
