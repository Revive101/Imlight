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
        public static readonly string ASK_IDENTIFY = "IDENTIFY_YOURSELF";

        protected SessionActor SessionActor { get; set; }

        public MessageService(SessionActor sessionActor)
        {
            this.SessionActor = sessionActor;
        }
         
        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();
            
            Receive<string>(x => x == ASK_IDENTIFY, x => 
            {
                Sender.Tell(new INTMSG_SERVICE_IDENTITY(this), Context.Self);
            });

            Receive<IInternalMessage>(internalMessage =>
            {
                // Find the method that handles this message type
                if (MessageHandlers.TryGetValue(internalMessage.GetType(), out var method))
                {
                    // Invoke the method with the message
                    method.Invoke(this, new object[] { internalMessage });
                }
                else
                {
                    // No handler for this message type
                    Unhandled(internalMessage);
                }
            });
        }

        protected void SendToSocket(INetworkMessage message)
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return;
            }

            SessionActor.Send(message);
        }

        protected void SendInternal(IInternalMessage msg)
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return;
            }

            SessionActor.ActorRef.Tell(msg);
        }

        protected T AskInternal<T>(IInternalMessage msg)
            where T : IInternalMessage
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"{this.GetType()} attempted to send message to undefined SessionActor.");
                return default(T);
            }

            var task = SessionActor.HandleInternalAsk<T>(msg);

            return task;
        }
        
        protected T AskServer<T>(INetworkMessage msg)
            where T : INetworkMessage
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
