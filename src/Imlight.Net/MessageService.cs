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
    public abstract class MessageService : ReceiveActor
    {
        public static readonly string ASK_IDENTIFY = "IDENTIFY_YOURSELF";

        protected SessionActor SessionActor { get; set; }
        public virtual Dictionary<Type, MethodInfo> MessageHandlers { get; private set; }

        public MessageService(SessionActor sessionActor)
        {
            this.SessionActor = sessionActor;

            SetMessageHandlers();
            ConfigureReceivers();
        }
         
        protected virtual void ConfigureReceivers()
        {
            Receive<string>(x => x == ASK_IDENTIFY, x => 
            {
                Sender.Tell(new INTMSG_SERVICE_IDENTITY(this), Context.Self);
            });

            Receive<INetworkMessage>(message =>
            {
                // Find the method that handles this message type
                if (MessageHandlers.TryGetValue(message.GetType(), out var method))
                {
                    // Invoke the method with the message
                    method.Invoke(this, new object[] { message });
                }
                else
                {
                    // No handler for this message type
                    Unhandled(message);
                }
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

            SessionActor.GetActorRef().Tell(msg);
        }

        protected void SendCloseSession()
        {
            SessionActor.GetActorRef().Tell("Close");
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

        private void SetMessageHandlers()
        {
            MessageHandlers = new Dictionary<Type, MethodInfo>();

            // Get all methods in this actor with a message handling attribute
            var methods = this
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttribute<MessageHandlerAttribute>() != null);

            if (methods.Count() <= 0)
            {
                Log.Logger.Warning($"{this.GetType()} does not have any methods with attribute {nameof(MessageHandlerAttribute)}." +
                    $"Is this intended behavior?");

                return;
            }

            foreach (var method in methods)
            {
                var paramType = method.GetParameters()[0].ParameterType;
                MessageHandlers.Add(paramType, method);
            }
        }
    }
}
