using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Realm
{
    internal class RealmDataReceivedEventArgs : EventArgs
    {

        public string Data { get; }

        // Constructor
        public RealmDataReceivedEventArgs(string data)
        {
            this.Data = data;
        }

    }
}
