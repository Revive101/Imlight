/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class InfractionCollection {

    public const string CollectionName = "Infractions";
    private const string BannedMachinesCollection = "BannedMachineIDs";
    private const string BannedIpsCollection = "BannedIPs";
    private static readonly IDocumentStore s_store;

    static InfractionCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Checks if a machine is banned based on its machine ID.
    /// </summary>
    /// <param name="machineId">The machine ID to check.</param>
    /// <returns>True if the machine is banned, false otherwise.</returns>
    public static bool IsMachineBanned(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = session.Query<MachineBanRecord>(collectionName: BannedMachinesCollection)
            .Where(x => x.MachineId == machineId && x.BanExpiration > DateTime.UtcNow)
            .FirstOrDefault();

        return bannedMachine != null;
    }

    /// <summary>
    /// Adds a machine ban record to the collection.
    /// </summary>
    /// <param name="machineId">The ID of the machine to be banned.</param>
    /// <param name="expiration">The expiration date and time of the ban.</param>
    public static void AddMachineBan(ulong machineId, DateTime expiration) {
        using var session = s_store.OpenSession();
        var bannedMachine = new MachineBanRecord(machineId, expiration);

        session.Store(bannedMachine);
        var metaData = session.Advanced.GetMetadataFor(bannedMachine);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = BannedMachinesCollection;

        session.SaveChanges();
    }

    /// <summary>
    /// Removes a machine ban record from the collection based on the specified machine ID.
    /// </summary>
    /// <param name="machineId">The ID of the machine to remove the ban for.</param>
    /// <returns>True if the machine ban record was successfully removed, false otherwise.</returns>
    public static bool RemoveMachineBan(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = session.Query<MachineBanRecord>(collectionName: BannedMachinesCollection)
            .Where(x => x.MachineId == machineId)
            .FirstOrDefault();

        if (bannedMachine != null) {
            session.Delete(bannedMachine);
            session.SaveChanges();
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an IP address is banned.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the IP address is banned, false otherwise.</returns>
    public static bool IsIpBanned(string ip) {
        using var session = s_store.OpenSession();
        var bannedIp = session.Query<IpBanRecord>(collectionName: BannedIpsCollection)
            .Where(x => x.Ip == ip && x.BanExpiration > DateTime.UtcNow)
            .FirstOrDefault();

        return bannedIp != null;
    }

    /// <summary>
    /// Adds an IP ban record to the collection.
    /// </summary>
    /// <param name="ip">The IP address to ban.</param>
    /// <param name="expiration">The expiration date and time of the ban.</param>
    public static void AddIpBan(string ip, DateTime expiration) {
        using var session = s_store.OpenSession();
        var bannedIp = new IpBanRecord(ip, expiration);

        session.Store(bannedIp);
        var metaData = session.Advanced.GetMetadataFor(bannedIp);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = BannedIpsCollection;

        session.SaveChanges();
    }

    /// <summary>
    /// Removes an IP ban record from the collection.
    /// </summary>
    /// <param name="ip">The IP address to remove from the ban list.</param>
    /// <returns>True if the IP ban record was successfully removed, false otherwise.</returns>
    public static bool RemoveIpBan(string ip) {
        using var session = s_store.OpenSession();
        var bannedIp = session.Query<IpBanRecord>(collectionName: BannedIpsCollection)
            .Where(x => x.Ip == ip)
            .FirstOrDefault();

        if (bannedIp != null) {
            session.Delete(bannedIp);
            session.SaveChanges();
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds an infraction to the collection.
    /// </summary>
    /// <param name="infraction">The infraction to add.</param>
    public static void AddInfraction(Infraction infraction) {
        using var session = s_store.OpenSession();

        session.Store(infraction);
        var metaData = session.Advanced.GetMetadataFor(infraction);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Removes an infraction from the collection based on the specified infraction ID.
    /// </summary>
    /// <param name="infractionId">The ID of the infraction to be removed.</param>
    public static void RemoveInfraction(ulong infractionId) {
        using var session = s_store.OpenSession();
        var infraction = session.Query<Infraction>(collectionName: CollectionName)
            .Where(x => x.InfractionId == infractionId)
            .FirstOrDefault();

        if (infraction != null) {
            session.Delete(infraction);
            session.SaveChanges();
        }
    }

    /// <summary>
    /// Updates an existing infraction with the provided infraction object.
    /// </summary>
    /// <param name="infraction">The infraction object containing the updated information.</param>
    public static void UpdateInfraction(Infraction infraction) {
        // Find the infraction by Id.
        using var session = s_store.OpenSession();
        var infractionToUpdate = session.Query<Infraction>(collectionName: CollectionName)
            .Where(x => x.InfractionId == infraction.InfractionId)
            .FirstOrDefault();

        // Update the infraction.
        infractionToUpdate.InfractionType = infraction.InfractionType;
        infractionToUpdate.InfractionTime = infraction.InfractionTime;
        infractionToUpdate.Reason = infraction.Reason;
        infractionToUpdate.Expiration = infraction.Expiration;
        infractionToUpdate.ResponsibleModerator = infraction.ResponsibleModerator;
        infractionToUpdate.WasWaived = infraction.WasWaived;
        infractionToUpdate.WasWaivedBy = infraction.WasWaivedBy;

        // Save the changes.
        session.SaveChanges();
    }

    /// <summary>
    /// Retrieves an infraction based on the provided infraction ID.
    /// </summary>
    /// <param name="infractionId">The ID of the infraction to retrieve.</param>
    /// <returns>The infraction object if found, otherwise null.</returns>
    public static Infraction GetInfraction(ulong infractionId) {
        using var session = s_store.OpenSession();
        var infraction = session.Query<Infraction>(collectionName: CollectionName)
            .Where(x => x.InfractionId == infractionId)
            .FirstOrDefault();

        return infraction;
    }

}
