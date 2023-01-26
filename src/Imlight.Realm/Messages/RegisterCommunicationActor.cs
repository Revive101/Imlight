using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.DML;

namespace Imlight.Realm.Messages
{
    internal class RegisterCommunicationActor
    {
        internal Socket Socket { get; init; }

        public RegisterCommunicationActor(Socket Socket)
        {
            this.Socket = Socket;
        }
    }
}
