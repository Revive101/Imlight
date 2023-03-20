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
            var position = new SharpDX.Vector4((short)message.LocationX * 4, (short)message.LocationY * 4, (short)message.LocationZ * 4, (short)message.Direction);
            var position2D = new SharpDX.Vector2(position.X, position.Y);

            if (zone == "WizardCity/WC_Hub") // TESTING ONLY
            {
                SharpDX.Vector2[] ravenwoodTrigger = {
                    new SharpDX.Vector2(-315.6f, 1663.56f),
                    new SharpDX.Vector2(147.6f, 1661.47f),
                    new SharpDX.Vector2(-208f, 1906.7f),
                    new SharpDX.Vector2(17.37f, 1906.7f),
                };
                var inTrigger = InsideOfPolygon(ravenwoodTrigger, ravenwoodTrigger.Length, position2D);

                if (inTrigger) // Player is inside of trigger, transfer to new zone
                {
                    var zoneMsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUEST() { ZoneName = "WizardCity/WC_Ravenwood" };
                    var zoneAsk = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERREQUESTRSP>(zoneMsg);
                }
            }

            // Broadcast the move to all other players in the zone.
            BroadcastClientMove(message);
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
        private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message)
        {
            // Query the global ID from the attach server.
            // @todo: avoid message query?
            var msg = new ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECT();
            var globalId = AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECTRSP>(msg).GlobalID;

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

        // Assuming (for now) that no two zones are on top of each other, and collision can be checked by determining if player's position is within some (X, Y) area
        private static bool InsideOfPolygon(SharpDX.Vector2[] p, int n, SharpDX.Vector2 pos)
        {
            double angle = 0;
            SharpDX.Vector2 p1, p2;

            for (int i = 0; i < n; i++)
            {
                p1.X = p[i].X - pos.X;
                p1.Y = p[i].Y - pos.Y;
                p2.X = p[(i + 1) % n].X - pos.X;
                p2.Y = p[(i + 1) % n].Y - pos.Y;

                angle += Angle2D(p1.X, p1.Y, p2.X, p2.Y);
            }
            return (Math.Abs(Math.Abs(angle) - (Math.PI * 2)) < 0.01); //Some tolerance for rounding errors
        }

        private static double Angle2D(float x1, float y1, float x2, float y2)
        {
            double diff, theta1, theta2;

            theta1 = Math.Atan2(y1, x1);
            theta2 = Math.Atan2(y2, x2);
            diff = theta2 - theta1;
            while (diff > Math.PI)
                diff -= Math.PI * 2;
            while (diff < -Math.PI)
                diff += Math.PI * 2;

            return diff;
        }

        private void BroadcastClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            // Query the mobile ID from the attach server.
            // @todo: avoid message query?
            var msg = new ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECT();
            var mobileId = AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYLOCALGAMEOBJECTRSP>(msg).MobileId;

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
    }
}
