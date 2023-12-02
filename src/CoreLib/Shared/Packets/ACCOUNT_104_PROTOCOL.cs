/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Packets;

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
