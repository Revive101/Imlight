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

        [InternalMessageHandler(typeof(INTMSG_SETACCOUNT))]
        private void InternalReceiveSetAccount(INTMSG_SETACCOUNT message)
        {
            this.Account = message.Account;
        }
    }
}
