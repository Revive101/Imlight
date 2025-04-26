/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// Interface for server protocol implementations.
/// </summary>
public interface IServerProtocol {
    
    byte ServiceID { get; }
    string ProtocolType { get; }
    int ProtocolVersion { get; }
    string ProtocolDescription { get; }
    
}

/// <summary>
/// Interface for server message implementations.
/// </summary>
public interface IServerMessage {
    
    byte MessageOrder { get; }
    byte ServiceID { get; }
    
}
