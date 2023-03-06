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

        /// <summary>
        /// A HashSet of the messages this service is capable of handling.
        /// </summary>
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
                Sender.Tell(new ServiceIdentityReply(this), Context.Self);
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

            // Any other object is considered an internal message.
            // @fixme: Instead of object, let's use type 'InternalMessage'.
            Receive<object>(internalMessage =>
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
                Log.Logger.Error($"ControlServiceActor attempted to send message to undefined SessionActor.");
                return;
            }

            SessionActor.Send(message);
        }

        protected void SendInternal(object msg)
        {
            if (SessionActor is null)
            {
                Log.Logger.Error($"ControlServiceActor attempted to send message to undefined SessionActor.");
                return;
            }

            SessionActor.GetActorRef().Tell(msg);
        }

        private void SetMessageHandlers()
        {
            MessageHandlers = new Dictionary<Type, MethodInfo>();

            // Get all methods in this actor with a message handling attribute
            var methods = this
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttribute<MessageHandlerAttribute>() != null);

            foreach (var method in methods)
            {
                var paramType = method.GetParameters()[0].ParameterType;
                MessageHandlers.Add(paramType, method);
            }
        }
    }
}
