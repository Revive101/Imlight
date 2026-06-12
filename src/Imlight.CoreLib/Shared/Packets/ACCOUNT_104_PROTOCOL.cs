/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Packets;

public sealed class ACCOUNT_104_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 104;
    public string ProtocolType { get; } = "ACCOUNT";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Server Account Messages.";

    public class MSG_QUERYACCOUNT : IServerMessage {
        
        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 104;

    }

    public class MSG_ACCOUNT : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 104;

        public Account Account;

    }

}
