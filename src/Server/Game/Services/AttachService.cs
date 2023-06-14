using Akka.Actor;
using Imlight.Common.Serializable;
using WizUnraveler;
using WizUnraveler.Cache;
using static WizUnraveler.Cache.TypeCache;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.IO;

namespace Imlight.Server.Game.Services
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
            if (!GetCharacter(account, message.CharID, out var character))
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
            
            // This is the first authentication action the user will send on the game server. Send some service
            // details using what we received here.
            SetAccountInternally(account);
            SetCharacterInternally(character);
            
            // Tell the game server that the user has attached, and now we need to find a zone process for their
            // zone, or create a new one.
            var zoneDetails = GetZoneDetails(message.ZoneName);
            if (zoneDetails.ErrorCode != 0)
            {
                SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED() { Error = zoneDetails.ErrorCode });
                return;
            }

            // Serialize the character's game object and send login complete.
            var charGameObject = character.GetWizClientObject();
            var localGameObjectData = new CoreObjectSerializer().Serialize(charGameObject);
            var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE()
            {
                RealmName = "Imlight",
                
                // Set character data.
                Data                = localGameObjectData,
                IsCSR               = (int)account.AuthLevel >= 3 ? 1 : 0,
                Permissions         = 31679, // @todo: these permissions look like bitflags. Find out what they mean.
       
                // Set zone data.
                ZoneName            = message.ZoneName,
                ZoneID              = message.ZoneID,
                DynamicZoneID       = zoneDetails.DynamicZoneId,
                DynamicServerProcID = zoneDetails.DynamicZoneId,
                
                // Misc
                ShowSubscriberIcon = 0,
                TestServer = 0
            };
            
            SendToSocket(loginCompleteMsg);
            AddPlayerToZone(charGameObject);
        }

        private ZONE_102_PROTOCOL.MSG_QUERYZONERSP GetZoneDetails(string zoneName)
        {
            // When we send a zone transfer request, it will also add the player to that zone.
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONE { ZoneName = zoneName, };
            return AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(zoneMsg);
        }

        private void AddPlayerToZone(WizClientObject charObj)
        {
            var msg = new ZONE_102_PROTOCOL.MSG_ADDPLAYER
            {
                Player = SessionActor.ActorRef,
                PlayerObject = charObj
            };
            SendToSessionServices(msg);
        }

        private bool GetCharacter(Account account, ulong charId, out Character character)
        {
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
                UserID = userId,
                SessionActor = SessionActor
            };
            var rsp = AskServer<SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP>(msg);

            account = rsp.Account;
            return rsp.ErrorCode == 0;
        }

        private void SetAccountInternally(Account account)
        {
            // Tell the SessionActor to set the account.
            SendToSessionServices(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT()
            {
                Account = account
            });
        }

        private void SetCharacterInternally(Character character)
        {
            SendToSessionServices(new CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER
            {
                Character = character
            });
        }
    }
}
