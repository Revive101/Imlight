using System.Linq;
using Imlight.CoreLib.Game.Models;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Implementations;

internal static class InfractionCollection {
    private const string BannedUsersCollection = "BannedUserIDs";
    private const string BannedMachinesCollection = "BannedMachineIDs";
    private static readonly IDocumentStore s_store;

    static InfractionCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    public static bool IsMachineBanned(ulong machineId) {
        using var session = s_store.OpenSession();
        var bannedMachine = session.Query<MachineBanRecord>(collectionName: BannedMachinesCollection)
            .Where(x => x.Id == machineId)
            .FirstOrDefault();

        return bannedMachine != null;
    }
}
