using System;
using System.Linq;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.WizardData.Models;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class InfractionCollection {
    public const string CollectionName = "Infractions";
    private const string BannedMachinesCollection = "BannedMachineIDs";
    private const string BannedIpsCollection = "BannedIPs";
    private static readonly IDocumentStore s_store;

    static InfractionCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    public static bool IsMachineBanned(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = session.Query<MachineBanRecord>(collectionName: BannedMachinesCollection)
            .Where(x => x.MachineId == machineId && x.BanExpiration > DateTime.UtcNow)
            .FirstOrDefault();

        return bannedMachine != null;
    }

    public static void AddMachineBan(ulong machineId, DateTime expiration) {
        using var session = s_store.OpenSession();
        var bannedMachine = new MachineBanRecord(machineId, expiration);

        session.Store(bannedMachine);
        var metaData = session.Advanced.GetMetadataFor(bannedMachine);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = BannedMachinesCollection;

        session.SaveChanges();
    }

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

    public static bool IsIpBanned(string ip) {
        using var session = s_store.OpenSession();
        var bannedIp = session.Query<IpBanRecord>(collectionName: BannedIpsCollection)
            .Where(x => x.Ip == ip && x.BanExpiration > DateTime.UtcNow)
            .FirstOrDefault();

        return bannedIp != null;
    }

    public static void AddIpBan(string ip, DateTime expiration) {
        using var session = s_store.OpenSession();
        var bannedIp = new IpBanRecord(ip, expiration);

        session.Store(bannedIp);
        var metaData = session.Advanced.GetMetadataFor(bannedIp);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = BannedIpsCollection;

        session.SaveChanges();
    }

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

    public static void AddInfraction(Infraction infraction) {
        using var session = s_store.OpenSession();

        session.Store(infraction);
        var metaData = session.Advanced.GetMetadataFor(infraction);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

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

    public static Infraction GetInfraction(ulong infractionId) {
        using var session = s_store.OpenSession();
        var infraction = session.Query<Infraction>(collectionName: CollectionName)
            .Where(x => x.InfractionId == infractionId)
            .FirstOrDefault();

        return infraction;
    }
}
