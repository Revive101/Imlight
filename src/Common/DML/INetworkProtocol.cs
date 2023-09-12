/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Common.DML;

public interface INetworkProtocol
{
    public byte ServiceId { get; }
    public string ProtocolType { get; }
    public int ProtocolVersion { get; }
    public string ProtocolDescription { get; }

    public INetworkMessage Dispatch(byte msgid);
}