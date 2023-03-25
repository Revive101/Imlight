using System.Collections.Generic;
using WizUnraveler.DML;

namespace Imlight.Net.Messages
{
    public sealed class SERVICE_101_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 104;
        public string ProtocolType { get; } = "ACCOUNT";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Server Account Messages.";

        public class MSG_QUERYMESSAGESERVICEIDENTITY : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 101;
        }

        public class MSG_MESSAGESERVICEIDENTITY : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 101;

            public MessageService Service;
        }
        
        public class MSG_QUERYUNLOADEDSERVICES : IServerMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 101;
        }
        
        public class MSG_QUERYLOADEDSERVICES : IServerMessage
        {
            public byte MessageOrder { get; } = 4;
            public byte ServiceID { get; } = 101;
        }
        
        public class MSG_SERVICESLIST : IServerMessage
        {
            public byte MessageOrder { get; } = 5;
            public byte ServiceID { get; } = 101;

            public List<System.Type> Services;
        }
        
        public class MSG_OPCODE_HALT : IServerMessage
        {
            public byte MessageOrder { get; } = 6;
            public byte ServiceID { get; } = 101;
        }
        
        public class MSG_OPCODE_RESUME : IServerMessage
        {
            public byte MessageOrder { get; } = 7;
            public byte ServiceID { get; } = 101;
        }
    }
}