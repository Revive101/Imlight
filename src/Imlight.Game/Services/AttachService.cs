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
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectSerializer;

namespace Imlight.Game.Services
{
    internal class AttachService : MessageService
    {
        private ulong globalId;
        private ulong permId;
        private ushort mobileId;

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
            
            // This is the first authentication action the user will send on the game server. Using the session key
            // given, we'll set the AccountService account to what the key is mapped to.
            SetAccountInternally(account);
            
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
                CriticalObjects     = zoneDetails.CriticalObjects,
            };
            
            SendToSocket(loginCompleteMsg);

            // Now, we need the newly connected player to see all the objects that exist in their zone.
            var zoneObjects = GetZoneObjects(message.ZoneName);
            SendZoneObjectsToClient(zoneObjects);

            // Now, broadcast to the zone of the new object, which is this player.
            BroadcastNewObjectCreation(charGameObject);
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECT))]
        private void ReceiveQueryLocalGameObject(ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECT message)
        {
            Sender.Tell(new ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECTRSP 
            { 
                GlobalID = globalId,
                PermID = permId,
                MobileId = mobileId 
            });
        }

        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECTRSP))]
        private void ReceiveCreateNetworkObjectRsp(ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECTRSP message)
        {
            this.globalId = message.GlobalID;
            this.permId = message.PermID;
            this.mobileId = message.MobileId;
        }

        private ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUESTRSP GetZoneDetails(string zoneName)
        {
            // When we send a zone transfer request, it will also add the player to that zone.
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUEST()
            {
                ZoneName = zoneName,
                SessionActor = SessionActor
            };
            return AskSessionServices<ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUESTRSP>(zoneMsg);
        }
        
        private List<CoreObject> GetZoneObjects(string zoneName)
        {
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTS();
            return AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTSRSP>(zoneMsg)?.CoreObjects;
        }

        private void SendZoneObjectsToClient(List<CoreObject> objects)
        {
            var serializer = new CoreObjectSerializer()
                .WithSerializerFlags(SerializerFlags.None)
                .WithPropertyFlags(PropertyFlags.Public | PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
            foreach (var obj in objects)
            {
                var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT()
                {
                    Data = serializer.Serialize(obj)
                };
                
                SendToSocket(msg);
            }
        }

        private void BroadcastNewObjectCreation(CoreObject obj)
        {
            var createObjectMsg = new ZONE_102_PROTOCOL.MSG_CREATENETWORKOBJECT()
            {
                Sender = SessionActor.ActorRef,
                CoreObject = obj,
                Selfless = true
            };
            SendToSessionServices(createObjectMsg);
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
    }
}
