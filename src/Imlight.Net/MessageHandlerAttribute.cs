using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MessageHandlerAttribute : Attribute
    {
        public Type MessageType { get; }

        public MessageHandlerAttribute(Type messageType)
        {
            MessageType = messageType;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class InternalMessageHandlerAttribute : MessageHandlerAttribute
    {
        public InternalMessageHandlerAttribute(Type messageType) : base(messageType) { }
    }
}
