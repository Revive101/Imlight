using Imlight.Common;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.WizardData.Models;

namespace Imlight.CoreLib.AntiAmbrose;

internal static class AuthorityRequester {
    internal static bool RequestAuthority(AuthLevel target, Account account, string reason = null) {
        if (account.AuthLevel < target) {
            Logger.Warning("Account {0} attempted authority request without auth level {1} ({2}) "
                + "This is considered suspicious behavior and will be logged.",
                Logger.Args(account.Username, target, reason ?? "No reason given"));

            account.AddInfraction(InfractionType.SuspiciousBehavior, "Attempted to use commands without auth level");
            return false;
        }
        else {
            return true;
        }
    }
}
