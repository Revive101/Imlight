using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.DML;

namespace Imlight.Net
{
    public abstract class MessageService : ReceiveProtocolDispatcher
    {
        protected SessionActor SessionActor { get; set; }

        public MessageService(SessionActor sessionActor)
        {
            this.SessionActor = sessionActor;
        }

        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY))]
        public void ReceiveMessageServiceIdentify(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY message)
        {
            var rsp = new SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY()
            {
                Service = this
            };
            
            Sender.Tell(rsp);
        }

        /// <summary>
        /// Sends a message directly to the socket.
        /// </summary>
        /// <param name="message"></param>
        protected void SendToSocket(INetworkMessage message)
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return;
            }

            SessionActor.ActorRef.Tell(message);
        }

        /// <summary>
        /// Sends the SessionActor a message. Used to send data to another service of the SessionActor.
        /// </summary>
        /// <param name="message"></param>
        protected void SendInternal(IServerMessage message)
        {
            SessionActor.ActorRef.Tell(message);
        }

        /// <summary>
        /// Asks the SessionActor for a return. Used to get data from another service of the SessionActor.
        /// </summary>
        /// <param name="message"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T AskInternal<T>(IServerMessage message)
            where T : IServerMessage
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return default(T);
            }
            
            if (message.ServiceID < 100)
            {
                throw new Exception($"You are sending a non-server message using {nameof(AskInternal)}! " +
                                    $"Do not do this. Use {nameof(SendToSocket)} instead.");
            }

            var task = SessionActor.HandleInternalAsk<T>(message);

            return task;
        }
        
        /// <summary>
        /// Asks the server the SessionActor is connected to.
        /// </summary>
        /// <param name="msg"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T AskServer<T>(IServerMessage msg)
            where T : IServerMessage
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return default(T);
            }

            var task = SessionActor.AskServer<T>(msg);

            return task;
        }
        
        protected void SendCloseSession()
        {
            SessionActor.ActorRef.Tell("Close");
        }
    }
}
