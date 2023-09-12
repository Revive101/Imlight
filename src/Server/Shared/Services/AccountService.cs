/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Server.Login.Models;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Shared.Services;

public class AccountService : MessageService
{
    public Account Account { get; private set; }

    public AccountService(SessionActor parentActor) : base(parentActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new AccountService(parentActor));
    }

    [InternalMessageHandler(typeof(ACCOUNT_104_PROTOCOL.MSG_ACCOUNT))]
    private void InternalReceiveSetAccount(ACCOUNT_104_PROTOCOL.MSG_ACCOUNT message)
    {
        this.Account = message.Account;
    }

    [InternalMessageHandler(typeof(ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT))]
    private void InternalReceiveGetAccount(ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT message)
    {
        Sender.Tell(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT()
        {
            Account = this.Account
        }, Context.Self);
    }
}