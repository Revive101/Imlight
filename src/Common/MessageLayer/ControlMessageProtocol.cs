namespace Imlight.Common.MessageLayer;

public class ControlMessageProtocol : MessageProtocol {
    public override byte ServiceId => 0;
    public override string ProtocolType => "CONTROL";
    public override int ProtocolVersion => 1;
    public override string ProtocolDescription => "Responsible for general network session management";

    public class SessionOffer : IMessage {
        public byte MessageOrder => 0;
        public byte ServiceId => 0;
        public byte AccessLevel => 0;

        [MessageElement("USHRT")] public ushort SessionId;
        [MessageElement("INT")] public int TimestampUpper;
        [MessageElement("INT")] public int TimestampLower;
        [MessageElement("UINT")] public uint Milliseconds;
    }

    public class KeepAlive : IMessage {
        public byte MessageOrder => 3;
        public byte ServiceId => 0;
        public byte AccessLevel => 0;

        [MessageElement("USHRT")] public ushort SessionId;
        [MessageElement("USHRT")] public ushort Milliseconds;
        [MessageElement("USHRT")] public ushort ElapsedSessionTime;
    }

    public class KeepAliveServer : IMessage {
        public byte MessageOrder => 3;
        public byte ServiceId => 0;
        public byte AccessLevel => 0;

        [MessageElement("USHRT")] public ushort SessionId;
        [MessageElement("UINT")] public uint Milliseconds;
    }

    public class KeepAliveResponse : IMessage {
        public byte MessageOrder => 4;
        public byte ServiceId => 0;
        public byte AccessLevel => 0;

        [MessageElement("USHRT")] public ushort SessionId;
        [MessageElement("USHRT")] public ushort Milliseconds;
        [MessageElement("USHRT")] public ushort ElapsedSessionTime;
    }

    public class SessionAccept : IMessage {
        public byte MessageOrder => 5;
        public byte ServiceId => 0;
        public byte AccessLevel => 0;

        [MessageElement("USHRT")] public ushort Reserved1;
        [MessageElement("INT")] public int TimestampUpper;
        [MessageElement("INT")] public int TimestampLower;
        [MessageElement("UINT")] public uint Milliseconds;
        [MessageElement("USHRT")] public ushort SessionId;
    }
}
