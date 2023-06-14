/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Services
{
    internal class MoveService : MessageService
    {
        private bool _isZoneTransferQueued;

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

            var character = GetActiveCharacter();
            var zone = character.CreationData.m_location;

            // Restore actual location information, as it is compressed by a factor of 4 and unsigned.
            // Yaw is represented in radians in the client, but transmitted to the server as degrees.
            var position = new SharpDX.Vector4(
                unchecked((short)message.LocationX * 4), 
                unchecked((short)message.LocationY * 4), 
                unchecked((short)message.LocationZ * 4), 
                message.Direction);
            var position2D = new SharpDX.Vector2(position.X, position.Y);

            // fixme: lazy fix
            if (_isZoneTransferQueued) return;

            if (zone == "WizardCity/WC_Hub") // TESTING ONLY
            {
                var ravenwoodBox = new Dictionary<SharpDX.Vector3[], string>()
                {
                    { new SharpDX.Vector3[] { 
                        new SharpDX.Vector3(-319, 1476, -100),
                        new SharpDX.Vector3(-315, 1663, -100),
                        new SharpDX.Vector3(163, 1660, -100),
                        new SharpDX.Vector3(165, 1468, -100),
                        new SharpDX.Vector3(-319, 1476, 100),
                        new SharpDX.Vector3(-315, 1663, 100),
                        new SharpDX.Vector3(163, 1660, 100),
                        new SharpDX.Vector3(165, 1468, 100)
                    }, "WizardCity/WC_Ravenwood" }
                };

                var circleTriggers = new Dictionary<SharpDX.Vector2, string>()
                {
                    { new SharpDX.Vector2(1370.5f, -3622.6f),   "WizardCity/WC_Shop_Area" },
                    { new SharpDX.Vector2(-961.6f, -2495.6f),   "WizardCity/Interiors/WC_Headmistress_House" },
                    { new SharpDX.Vector2(-1559.8f, -11.9f),    "WizardCity/Interiors/WC_Headmaster_Tower" },
                    { new SharpDX.Vector2(-6308f, 2649.3f),     "WizardCity/WC_Golem_Tower" },
                    { new SharpDX.Vector2(2649.5f, 5013.1f),    "WizardCity/WC_NightSide" },
                    { new SharpDX.Vector2(6256.8f, 5473.5f),    "WizardCity/WC_Streets/WC_Unicorn" },
                    { new SharpDX.Vector2(9722.9f, 2184.2f),    "WizardCity/Interiors/WC_Library" },
                    { new SharpDX.Vector2(8380.9f, -2126.3f),   "WizardCity/WC_Streets/Interiors/WC_PET_Park" },
                };

                CheckTransferPrisms(character, new SharpDX.Vector3(position.X, position.Y, position.Z), ravenwoodBox); 
                CheckTransferPoints(character, position2D, circleTriggers);

            } 
            else if (zone == "WizardCity/WC_Shop_Area")
            {
                var circleTriggers = new Dictionary<SharpDX.Vector2, string>()
                {
                    { new SharpDX.Vector2(-55f, 350f),          "WizardCity/WC_Hub" },
                    { new SharpDX.Vector2(-2075f, -5525f),      "WizardCity/WC_Streets/WC_Colossus" },
                    { new SharpDX.Vector2(-6095f, -3260f),      "WizardCity/WC_Streets/WC_OldeTown" },
                };

                CheckTransferPoints(character, position2D, circleTriggers);
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

        private Character GetActiveCharacter()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.Character;
        }

        private ZONE_102_PROTOCOL.MSG_QUERYZONERSP GetZoneDetails(string zoneName)
        {
            // When we send a zone transfer request, it will also add the player to that zone.
            var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONE { ZoneName = zoneName, };
            return AskSessionServices<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(zoneMsg);
        }

        private void CheckTransferPoints(Character character, SharpDX.Vector2 pos, Dictionary<SharpDX.Vector2, string> dict) 
        {
            foreach (var p in dict)
            {
                var isInside = Math.InsideOfCircle(p.Key, 175, pos); // This radius needs to be sourced elsewhere

                if (isInside) // Player is inside of trigger, transfer to new zone
                {  
                    SendZoneTransferSequence(character, p.Value);
                    return;
                }
            }
        }

        private void CheckTransferPolygons(Character character, SharpDX.Vector2 pos, Dictionary<SharpDX.Vector2[], string> dict)
        {
            foreach (var p in dict)
            {
                var isInside = Math.InsideOfPolygon(p.Key, pos);

                if (isInside) // Player is inside of trigger, transfer to new zone
                {
                    SendZoneTransferSequence(character, p.Value);
                    return;
                }
            }
        }

        private void CheckTransferPrisms(Character character, SharpDX.Vector3 pos, Dictionary<SharpDX.Vector3[], string> dict)
        {
            foreach (var p in dict)
            {
                var isInside = Math.InsideOfPrism(p.Key, pos);

                if (isInside) // Player is inside of trigger, transfer to new zone
                {
                    SendZoneTransferSequence(character, p.Value);
                    return;
                }
            }
        }

        private void SendZoneTransferSequence(Character character, string zoneName)
        {
            character.nextZone = zoneName;

            var zoneMsg = new ZONE_102_PROTOCOL.MSG_QUERYZONE() { ZoneName = zoneName };
            AskServer<ZONE_102_PROTOCOL.MSG_QUERYZONERSP>(zoneMsg);

            _isZoneTransferQueued = true;

            var transferRequest = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST() // Ask client if it's OK to transfer zone
            {
                SendAck = 0,
                ZoneName = zoneName
            };
            SendToSocket(transferRequest);
        }
    }
}
