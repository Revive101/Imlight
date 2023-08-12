/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Serializable;
using Imlight.Server.Game.Models;
using WizUnraveler;
using WizUnraveler.Cache;
using static WizUnraveler.Cache.TypeCache;
using Imlight.Server.Login.Models;
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
                SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED()
                {
                    Error = 1,
                    Rejected = 1,
                });
                throw new SessionFatalException(
                    $"User [{message.UserID}] failed to validate login key: {message.LoginKey}.");
            }
            if (!GetCharacterFromAccount(account, message.CharID, out var character))
            {
                SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED()
                {
                    Error = 1,
                    NoDisconnect = 1, // @todo: find out what these error codes mean.
                    Rejected = 1,
                });
                throw new SessionFatalException($"User [{message.UserID}] tried to attach with a character " +
                                                $"they did not have.");
            }
            
            // This is the first authentication action the user will send on the game server. Send messages to the
            // other services denoting both the account and character this SessionActor just logged into.
            SetAccountInternally(account);
            SetCharacterInternally(character);
            
            // Tell the game server that the user has attached, and now we need to find a zone process for their
            // zone, or create a new one.
            var zoneDetails = SendZoneTransfer(message.ZoneName);
            if (zoneDetails.ErrorCode != 0)
            {
                SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED { Error = zoneDetails.ErrorCode });
                return;
            }
            
            // Set the character's location and zone to the ones given in the message.
            character.SetZone(message.ZoneName);
            character.SetLocation(message.Location);

            // Serialize the character's game object.
            var charGameObject = CharacterObjectLoader.GetPlayerGameObject(ref character);
            charGameObject.m_nMobileID = zoneDetails.MobileId; // Set the mobile id to the one given by the zone.
            character.GameObject = charGameObject;
            var localGameObjectData = new CoreObjectSerializer().Serialize(charGameObject);
            if (charGameObject is null || string.IsNullOrEmpty(localGameObjectData))
                throw new ServiceRetryException($"User {message.UserID} failed to grab or deserialize " +
                                                $"their player object.");
            
            // Send login complete.
            var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE()
            {
                RealmName = "Imlight",
                
                // Set character data.
                Data                = localGameObjectData,
                IsCSR               = (int)account.AuthLevel >= 1 ? 1 : 0,
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

        private ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP SendZoneTransfer(string zoneName)
        {
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER
            {
                DestinationZone = zoneName, 
                SendToClient = false
            };
            return AskOtherService<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(zoneMsg);
        }

        private void AddPlayerToZone(WizClientObject charObj)
        {
            var msg = new ZONE_102_PROTOCOL.MSG_ADDPLAYER
            {
                Player = SessionActor.ActorRef,
                PlayerObject = charObj
            };
            TellOtherServices(msg);
        }

        private bool GetCharacterFromAccount(Account account, ulong charId, out Character character)
        {
            var result = account.GetCharacter(charId);
            character = result;

            return result is not null;
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
            TellOtherServices(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT()
            {
                Account = account
            });
        }

        private void SetCharacterInternally(Character character)
        {
            TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_SETACTIVECHARACTER
            {
                Character = character
            });
        }
    }
}
