using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using Imlight.Net.Messages;
using Imlight.Common;
using Imlight.Game;

namespace Imlight.Game.Services
{
    internal class MoveService : MessageService
    {
        public MoveService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new MoveService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
        private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            var account = GetSocketAccount();

            // If the socket account cannot be found, send the client an error.
            if (account is null)
            {
                Log.Logger.Error($"Service [{this.GetType()}] socket account could not be retrieved!");
                return;
            }

            var character = account.Characters[0]; // @todo: get active character
            var zone = character.CreationData.m_location;

            // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
            // Yaw is represented in radians in the client, but transmitted to the server as degrees.
            var position = new SharpDX.Vector4(
                unchecked((short)message.LocationX * 4), 
                unchecked((short)message.LocationY * 4), 
                unchecked((short)message.LocationZ * 4), 
                message.Direction);
            var position2D = new SharpDX.Vector2(position.X, position.Y);

            if (zone == "WizardCity/WC_Hub") // TESTING ONLY
            {
                SharpDX.Vector2[] ravenwoodTrigger = {
                    new SharpDX.Vector2(-315.6f, 1663.56f),
                    new SharpDX.Vector2(147.6f, 1661.47f),
                    new SharpDX.Vector2(-208f, 1906.7f),
                    new SharpDX.Vector2(17.37f, 1906.7f),
                };
                var inTrigger = IMath.InsideOfPolygon(ravenwoodTrigger, ravenwoodTrigger.Length, position2D);

                if (inTrigger) // Player is inside of trigger, transfer to new zone
                {
                    var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONE() { ZoneName = "WizardCity/WC_Ravenwood" };
                    var zoneAsk = AskServer<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(zoneMsg);

                    var transferRequest = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST() // Ask client if it's OK to transfer zone
                    {
                        SendAck = 0,
                        ZoneName = "WizardCity/WC_Ravenwood"
                    };
                    SendToSocket(transferRequest);
                }
            }

            // Broadcast the move to all other players in the zone.
            BroadcastClientMove(message);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
        private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message)
        {
            var globalId = GetActiveCoreObject().m_globalID;
            
            var stateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE()
            {
                NewState = message.NewState,
                GlobalID = globalId
            };
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = stateMsg,
                Selfless = true
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_JUMP))]
        private void ReceiveClientJump(GAME_5_PROTOCOL.MSG_JUMP message)
        {
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = message,
                Selfless = false,
            });
        }

        private void BroadcastClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            // Query the mobile ID from the CharacterService
            var mobileId = GetActiveCoreObject().m_nMobileID;
            
            var serverMoveMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE
            {
                LocationX = message.LocationX,
                LocationY = message.LocationY,
                LocationZ = message.LocationZ,
                Direction = message.Direction,
                MobileID = mobileId,
            };
            var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = serverMoveMsg,
                Selfless = true,
            };
            SendToSessionServices(broadcastMsg);
        }

        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }
        
        private ZONE_102_PROTOCOL.MSG_QUERYZONERSP GetZoneDetails(string zoneName)
        {
            // When we send a zone transfer request, it will also add the player to that zone.
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONE { ZoneName = zoneName, };
            return AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(zoneMsg);
        }
    }
}
