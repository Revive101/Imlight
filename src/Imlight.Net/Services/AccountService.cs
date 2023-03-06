using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Data;
using Imlight.Net.Messages;
using WizUnraveler.Cache;

namespace Imlight.Net.Services
{
    public class AccountService : MessageService
    {
        public Account Account { get; private set; }

        public AccountService(SessionActor parentActor) : base(parentActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new AccountService(parentActor));
        }

        [InternalMessageHandler(typeof(INTERN_ACCOUNT_PROTOCOL.INTMSG_SETACCOUNT))]
        private void InternalReceiveSetAccount(INTERN_ACCOUNT_PROTOCOL.INTMSG_SETACCOUNT message)
        {
            this.Account = message.Account;
        }

        [InternalMessageHandler(typeof(INTERN_ACCOUNT_PROTOCOL.INTMSG_GETACCOUNT))]
        private void InternalReceiveGetAccount(INTERN_ACCOUNT_PROTOCOL.INTMSG_GETACCOUNT message)
        {
            Sender.Tell(new INTERN_ACCOUNT_PROTOCOL.INTMSG_ACCOUNT()
            {
                Account = this.Account
            });
        }
    }
}
