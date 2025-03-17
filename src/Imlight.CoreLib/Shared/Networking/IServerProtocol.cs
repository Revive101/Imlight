/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.Shared.Networking;

public interface IServerProtocol {
    public byte ServiceID { get; }
    public string ProtocolType { get; }
    public int ProtocolVersion { get; }
    public string ProtocolDescription { get; }
}
