using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using System.Collections.Generic;

namespace Imlight.Server.Game.Services
{
    public class ChatService : MessageService
    {
        public ChatService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ChatService(parentActor));
        }
        
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
        private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message)
        {
            var globalId = GetActiveCoreObject().m_globalID;

            var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT()
            {
                Message = message.Message,
                SourceID = globalId,
                SourceName = "Tester"
            };
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = msg,
                Selfless = true
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
        private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message)
        {
            var globalId = GetActiveCoreObject().m_globalID;

            var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT()
            {
                MessageID = message.MessageID,
                SourceID = globalId,
                SourceName = "Tester"
            };
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = msg,
                Selfless = true
            });

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK()
            {
                SpellID = 2066
            });

            SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK()
            {
                SpellID = 949570680
            });


            SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK()
            {
                SpellID = 2537945
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQASKSERVER))]
        private void ReceiveRequest(GAME_5_PROTOCOL.MSG_REQASKSERVER message)
        {
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CORE_EMOTE))]
        private void ReceiveCoreEmote(GAME_5_PROTOCOL.MSG_CORE_EMOTE message)
        {
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = message,
                Selfless = true,
            });
        }
        
        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }
    }
}