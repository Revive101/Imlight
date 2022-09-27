using System.Linq;
using System.Globalization;
using System;
using System.Collections.Generic;

/*
Realm
Realms are the bread and butter of this server structure. 
They hold current players, worlds, and zones. 
They are the primary communicator with clients.

For better elaboration, see the RealmManager diagram:
https://app.diagrams.net/#G17utqstWzrlxPp8cVjTZX4e_Hhy8ThKSn
*/

namespace Imlight.Realm
{
    public class RealmManager
    {

        public List<Realm> Realms = new List<Realm>();

        /// <summary>
        /// Creates a new realm.
        /// </summary>
        /// <param name="name">The name of the new realm.</param>
        /// <param name="autoStart">Should the realm automatically start the TCP listener?</param>
        public void CreateRealm(string name)
        {
            if (!this.doesRealmExist(name))
            {
                Realm newRealm = new Realm(name, true);
                this.Realms.Add(newRealm);
            }
        }

        /// <summary>
        /// Removes a realm by name.
        /// </summary>
        /// <param name="name">The name of the realm to delete.</param>
        public void RemoveRealm(string name)
        {
            if (this.doesRealmExist(name))
            {
                Realm realm = Realms.FirstOrDefault(x => x.Name == name);
                this.Realms.Remove(realm);
            }
            else
            {
                //@TODO: Log error here
            }
        }

        /// <summary>
        /// Stops all existing realms.
        /// </summary>
        public void StopAllRealms()
        {
            // Stopping a realm simply stops the server. The realm isn't destroyed.
            for (int i = 0; i < Realms.Count; i++)
            {
                if (Realms[i].IsOpen()) Realms[i].StopServer();
                else continue;
            }
        }

        /// <summary>
        /// Stops and deletes all existing realms.
        /// </summary>
        public void DisposeAllRealms()
        {
            // The be-all-end-all. Servers are stopped and deleted with this method.
            for (int i = 0; i < Realms.Count; i++)
            {
                if (Realms[i].IsOpen()) Realms[i].Dispose();

                // The GC will automatically delete the realm for us.
                Realms.RemoveAt(i);
            }
        }

        /// <summary>
        /// Determines whether a realm exists by name.
        /// </summary>
        /// <param name="name">The name of the realm to search.</param>
        /// <returns>True, if a realm is found by that name. Flase otherwise.</returns>
        private bool doesRealmExist(string name)
        {
            Realm r = this.Realms.First(realm => realm.Name == name);
            return !(r != null);
        }

    }
}
