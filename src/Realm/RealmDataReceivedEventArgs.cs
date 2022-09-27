using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Realm
{
    //@TODO: This class should soon be expanded to carry received arguments such as:
    // - The time of arrival
    // - The socket itself
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
