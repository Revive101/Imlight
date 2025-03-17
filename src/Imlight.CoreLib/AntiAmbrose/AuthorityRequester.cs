/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.AntiAmbrose;

internal static class AuthorityRequester {

    /// <summary>
    /// Checks if the account has the required authority level to perform the action.
    /// </summary>
    /// <param name="target">The required authority level.</param>
    /// <param name="account">The account to check.</param>
    /// <param name="reason">The reason for the authority request.</param>
    /// <returns>True if the account has the required authority level, false otherwise.</returns>
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
