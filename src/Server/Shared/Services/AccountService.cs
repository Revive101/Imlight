using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Shared.Services
{
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
}
