using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using Imlight.Net.Messages;
using Imlight.Resources;

namespace Imlight.Game
{
    public class GameWorld : ReceiveProtocolDispatcher
    {
        public Dictionary<string, IActorRef> Zones { get; }

        private GameServer _server;
        
        public GameWorld(GameServer server)
        {
            this.Zones = new Dictionary<string, IActorRef>();
            this._server = server;
            
            Log.Logger.Information("Game world created.");
        }
        
        public static Props Props(GameServer server)
        {
            return Akka.Actor.Props.Create(() => new GameWorld(server));
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONE))]
        private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_QUERYZONE message)
        {
            var response = new ZONE_102_PROTOCOL.MSG_QUERYZONERSP();
            
            // First, make sure this zone is valid by checking the AccessPassManager.
            if (!AccessPassManager.IsZone(message.ZoneName))
            {
                Log.Logger.Error(
                    $"GameWorld received invalid zone transfer request from {Sender.Path.Name}.");
                
                response.ErrorCode = 1;
                Sender.Tell(response);

                return;
            }
            
            // Get the zone if it's already loaded; or, create a new one if it's not.
            IActorRef zone;
            if (!Zones.ContainsKey(message.ZoneName))
            {
                // '/' is an illegal character in Akka.NET actor names, so we replace it with '@'.
                var zoneActorName = message.ZoneName
                    .Replace('/', '@');
                
                zone = Context.ActorOf(Zone.Props(message.ZoneName), zoneActorName);
                Zones.Add(message.ZoneName, zone);
                
                // Log the new zone creation.
                Log.Logger.Information($"GameWorld created new zone: {message.ZoneName}");
            }
            else
            {
                zone = Zones[message.ZoneName];
            }
            
            // Query the zone for it's details.
            var zoneQueryMessage = new ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILS();
            var zoneQueryResponse = zone
                .Ask<ZONE_102_PROTOCOL.MSG_QUERYZONEDETAILSRSP>(zoneQueryMessage)
                .Result;

            // Send the response back to the client.
            response.NewZone = zone;
            response.CriticalObjects = zoneQueryResponse.CriticalObjects;
            response.PlayerObjects = zoneQueryResponse.PlayerObjects;
            response.DynamicZoneId = zoneQueryResponse.DynamicZoneId;
            response.ErrorCode = 0;
            Sender.Tell(response);
        }
    }
}