using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net
{
    public class RegisterCommunicationActor
    {
        public Socket Socket { get; init; }

        public RegisterCommunicationActor(Socket Socket)
        {
            this.Socket = Socket;
        }
    }
}
