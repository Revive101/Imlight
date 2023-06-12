using Imlight.Server.Shared.Networking;
using Imlight.Server.Database;

namespace Imlight.Server.Shared.Packets
{
    public sealed class ACCOUNT_104_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 104;
        public string ProtocolType { get; } = "ACCOUNT";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Server Account Messages.";

        public class MSG_QUERYACCOUNT : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 104;
        }
        
        public class MSG_ACCOUNT : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 104;
            
            public Account Account;
        }
    }
}
