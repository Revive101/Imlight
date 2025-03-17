/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Services;

public class AccountService : MessageService {
    public Account Account { get; private set; }

    public AccountService(SessionActor parentActor) : base(parentActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new AccountService(parentActor));
    }

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
