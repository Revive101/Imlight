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

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Services;

internal class AccountService(SessionActor parentActor) : MessageService(parentActor) {
    
    public Account Account { get; private set; }

    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new AccountService(parentActor));

    [InternalMessageHandler(typeof(ACCOUNT_104_PROTOCOL.MSG_ACCOUNT))]
    private void InternalReceiveSetAccount(ACCOUNT_104_PROTOCOL.MSG_ACCOUNT message) {
        this.Account = message.Account;
        this.Account.SessionActor = this.SessionActor;
    }

    [InternalMessageHandler(typeof(ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT))]
    private void InternalReceiveGetAccount(ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT message) {
        Sender.Tell(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT() {
            Account = this.Account
        }, Context.Self);
    }

}
