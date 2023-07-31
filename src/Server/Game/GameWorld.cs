/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game
{
    public class GameWorld : ReceiveProtocolDispatcher
    {
        public Dictionary<string, IActorRef> Zones { get; }

        private GameServer _server;
        
        public GameWorld(GameServer server)
        {
            this.Zones = new Dictionary<string, IActorRef>();
            this._server = server;
        }
        
        public static Props Props(GameServer server)
        {
            return Akka.Actor.Props.Create(() => new GameWorld(server));
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
        private void ReceiveZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
        {
            // First, make sure this zone is valid by checking the AccessPassManager.
            if (!AccessPassManager.DoesZoneExist(message.ZoneName))
            {
                Log.Error("{Name} received invalid zone name {ZoneName}",
                    Log.Args(nameof(GameWorld), message.ZoneName));
                
                var response = new ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP();
                response.ErrorCode = 1;
                Sender.Tell(response);

                return;
            }
            
            // Get the zone if it's already loaded; or, create a new one if it's not.
            IActorRef zone;
            if (!Zones.ContainsKey(message.ZoneName))
            {
                // '/' is an illegal character in Akka.NET actor names, so we replace it with '-'.
                var zoneActorName = message.ZoneName
                    .Replace('/', '-');
                
                zone = Context.ActorOf(Zone.WizardZone.Props(message.ZoneName), zoneActorName);
                Zones.Add(message.ZoneName, zone);
                
                // Log the new zone creation.
                Log.Information("GameWorld created new zone: {ZoneName}",
                    Log.Args(message.ZoneName));
            }
            else
            {
                zone = Zones[message.ZoneName];
            }
            
            // Forward the message to the zone actor we just created.
            zone.Forward(message);
        }
    }
}