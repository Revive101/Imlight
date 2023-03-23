using Imlight.Data;
using WizUnraveler.Cache;

namespace Imlight.Net.Messages
{
    public class CHARACTER_103_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 103;
        public string ProtocolType { get; } = "CHARACTER";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Character General Messages.";
        
        public class MSG_SETACTIVECHARACTER : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 103;
            
            public Character Character;
        }
        
        public class MSG_QUERYACTIVECHARACTER : IServerMessage
        {
            public byte MessageOrder { get; } = 2;
            public byte ServiceID { get; } = 103;
        }
        
        public class MSG_CHARACTER : IServerMessage
        {
            public byte MessageOrder { get; } = 3;
            public byte ServiceID { get; } = 103;

            public Character Character;
            public TypeCache.CoreObject CharacterObject;
        }
    }
}