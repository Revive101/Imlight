/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Common.DML;

public interface INetworkMessage
{
    public byte MessageOrder { get; }
    public byte ServiceId { get; }
}