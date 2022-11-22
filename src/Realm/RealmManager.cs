using System.Linq;
using System.Globalization;
using System;
using System.Collections.Generic;
using Imlight.Common.Logger;
using Imlight.Common;

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

        // Realm dictionary: [ID]: RealmObject
        public Dictionary<sbyte, Realm> Realms = new Dictionary<sbyte, Realm>();

        /// <summary>
        /// Creates a new realm.
        /// </summary>
        /// <param name="name">The name of the new realm.</param>
        /// <param name="autoStart">Should the realm automatically start the TCP listener?</param>
        public void CreateRealm(string name)
        {
            if (!this.doesRealmExist(name))
            {
                sbyte Id = RandomGen.Unused.SignedNumber(Realms.Keys);
                Realm newRealm = new Realm(name, Id, true);
                this.Realms.Add(Id, newRealm);

                Log.Info($"New realm \"{name}\" created.");
            }
            else
            {
                Log.Error($"Attempted to create realm by name \"{name}\", but a realm by that name already exists!");
                return;
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
                Realm realm = Realms.Values.FirstOrDefault(x => x.Name == name);
                this.Realms.Remove(realm.Id);

                Log.Info($"Realm \"{name}\" removed.");
            }
            else
            {
                Log.Error($"Attempted to remove realm by name \"{name}\", but a realm by that name was not found!");
                return;
            }
        }

        /// <summary>
        /// Stops all existing realms.
        /// </summary>
        public void StopAllRealms()
        {
            Log.Info("Stopping all realms..");

            // Stopping a realm simply stops the server. The realm isn't destroyed.
            foreach (Realm realm in Realms.Values)
            {
                if (realm.IsOpen()) realm.StopServer();
                else continue;
            }
        }

        /// <summary>
        /// Stops and deletes all existing realms.
        /// </summary>
        public void DisposeAllRealms()
        {
            Log.Warn("Disposing all realms..");

            // The be-all-end-all. Servers are stopped and deleted with this method.
            foreach (Realm realm in Realms.Values)
            {
                realm.Dispose();

                // The GC will automatically delete the realm for us.
                Realms.Remove(realm.Id);
            }
        }

        /// <summary>
        /// Determines whether a realm exists by name.
        /// </summary>
        /// <param name="name">The name of the realm to search.</param>
        /// <returns>True, if a realm is found by that name. Flase otherwise.</returns>
        private bool doesRealmExist(string name)
        {
            try 
            {
                Realm r = this.Realms.Values.First(realm => realm.Name == name);
                return !(r != null);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }
}
