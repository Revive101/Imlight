using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using Imlight.Net;
using WizUnraveler;

namespace Imlight.Login
{
    public class RealmManagerActor : ServerReceiverActor
    {
        public RealmManagerActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) { }


    }
}
