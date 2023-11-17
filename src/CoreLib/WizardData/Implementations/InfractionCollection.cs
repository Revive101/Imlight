using System.Linq;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.WizardData.Models;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class InfractionCollection {
    public const string CollectionName = "Infractions";
    private const string BannedMachinesCollection = "BannedMachineIDs";
    private static readonly IDocumentStore s_store;

    static InfractionCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    public static bool IsMachineBanned(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = session.Query<MachineBanRecord>(collectionName: BannedMachinesCollection)
            .Where(x => x.MachineId == machineId)
            .FirstOrDefault();

        return bannedMachine != null;
    }

    public static void AddMachineBan(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = new MachineBanRecord {
            MachineId = machineId
        };

        session.Store(bannedMachine);
        var metaData = session.Advanced.GetMetadataFor(bannedMachine);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = BannedMachinesCollection;

        session.SaveChanges();
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

    public static Infraction GetInfraction(ulong infractionId) {
        using var session = s_store.OpenSession();
        var infraction = session.Query<Infraction>(collectionName: CollectionName)
            .Where(x => x.InfractionId == infractionId)
            .FirstOrDefault();

        return infraction;
    }
}
