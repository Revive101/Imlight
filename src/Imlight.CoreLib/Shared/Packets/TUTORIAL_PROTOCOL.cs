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

namespace Imlight.CoreLib.Shared.Packets;

public sealed class TUTORIAL_108_PROTOCOL : IServerProtocol {

    public byte ServiceID { get; } = 108;
    public string ProtocolType { get; } = "TUTORIAL";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Tutorial Messages.";

    public class MSG_TUTORIALREBUILDDUELHAND : IServerMessage {

        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 108;

        /// <summary>Spell template IDs to place into the recipient's combat hand.</summary>
        public uint[] SpellIdsToGrant;

        /// <summary>1 = the sending player; any other value = a duel creature with that template ID.</summary>
        public ulong RecipientTemplateId;

    }

    public class MSG_TUTORIALGRANTPIPS : IServerMessage {

        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 108;

        public int Count;

    }

}
