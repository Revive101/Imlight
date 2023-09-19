/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Common.DML;

public class ControlMessages : INetworkProtocol
{

    public byte ServiceId => 0;
    public string ProtocolType => "CONTROL";
    public int ProtocolVersion => 1;
    public string ProtocolDescription => "Responsible for general network session management";

    public class SessionOffer : INetworkMessage
    {
        public byte MessageOrder => 0;
        public byte ServiceId => 0;
        [DmlElement(DmlType.USHRT)] public ushort SessionId;
        [DmlElement(DmlType.INT)] public int TimestampUpper;
        [DmlElement(DmlType.INT)] public int TimestampLower;
        [DmlElement(DmlType.UINT)] public uint Milliseconds;
    }

    public class KeepAlive : INetworkMessage
    {
        public byte MessageOrder => 3;
        public byte ServiceId => 0;
        [DmlElement(DmlType.USHRT)] public ushort SessionId;
        [DmlElement(DmlType.USHRT)] public ushort Milliseconds;
        [DmlElement(DmlType.USHRT)] public ushort ElapsedSessionTime;
    }

    public class KeepAliveServer : INetworkMessage
    {
        public byte MessageOrder => 3;
        public byte ServiceId => 0;
        [DmlElement(DmlType.USHRT)] public ushort SessionId;
        [DmlElement(DmlType.UINT)] public uint Milliseconds;
    }

    public class KeepAliveResponse : INetworkMessage
    {
        public byte MessageOrder => 4;
        public byte ServiceId => 0;
        [DmlElement(DmlType.USHRT)] public ushort SessionId;
        [DmlElement(DmlType.USHRT)] public ushort Milliseconds;
        [DmlElement(DmlType.USHRT)] public ushort ElapsedSessionTime;
    }

    public class SessionAccept : INetworkMessage
    {
        public byte MessageOrder => 5;
        public byte ServiceId => 0;
        [DmlElement(DmlType.USHRT)] public ushort Reserved1;
        [DmlElement(DmlType.INT)] public int TimestampUpper;
        [DmlElement(DmlType.INT)] public int TimestampLower;
        [DmlElement(DmlType.UINT)] public uint Milliseconds;
        [DmlElement(DmlType.USHRT)] public ushort SessionId;
    }

    public INetworkMessage Dispatch(byte id)
    {
        return id switch
        {
            (0) => new SessionOffer(),
            (3) => new KeepAlive(),
            (4) => new KeepAliveResponse(),
            (5) => new SessionAccept(),
            _ => throw new InternalException($"Control message by ID [{id}] was not found!"),
        };
    }

}