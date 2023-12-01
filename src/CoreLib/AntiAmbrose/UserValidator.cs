using Imlight.Common.Cryptography;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using System;
using static Imlight.Common.Caches.LOGIN_7_PROTOCOL;

namespace Imlight.CoreLib.AntiAmbrose;

internal enum UserValidateResult {
    Success = 0,
    AccountBanned = 87620544,
    MachineBanned = 1157331960,
    ValidateFailed = 246825817,
    Timeout = 1361855231,
}

/// <summary>
/// This static class is responsible for validating users. Validation happens when a client is already logged in and is starting the game
/// as opposed to <see cref="UserAuthenticator"/>, which happens during the login process.
/// </summary>
internal static class UserValidator {
    internal class ValidationDetails {
        internal Account _account;
        internal string _sessionKey;
        internal UserValidateResult _result;
    }

    internal static ValidationDetails Validate(SessionActor sessionActor, MSG_USER_VALIDATE validateMessage) {
        var details = new ValidationDetails();

        // Try getting the account from the message's UserID.
        var matchedAccount = AccountCollection.GetAccount(validateMessage.UserID);
        if (matchedAccount is null) {
            details._result = UserValidateResult.ValidateFailed;
            return details;
        }

        // Check to see if this account is banned.
        if (matchedAccount.InfractionHistory.IsCurrentlyBanned || matchedAccount.IsLocked) {
            details._result = UserValidateResult.AccountBanned;
            return details;
        }

        // Check to see if this machine is banned.
        if (InfractionCollection.IsMachineBanned(validateMessage.MachineID)) {
            // Add an infraction to the account.
            matchedAccount.AddInfraction(InfractionType.Warn, "Logged in with banned machine ID.", null);

            details._result = UserValidateResult.MachineBanned;
            return details;
        }

        // Check to see if this IP is banned.
        if (InfractionCollection.IsIpBanned(sessionActor.Ip)) {
            // Add an infraction to the account.
            matchedAccount.AddInfraction(InfractionType.Warn, "Logged in with banned IP.", null);

            details._result = UserValidateResult.MachineBanned;
            return details;
        }

        // Validation happens after authentication, so we need to check if the session key matches.
        var sessionKey = ClientKeyCollection.GetSessionKey(matchedAccount.AccountId, validateMessage.MachineID);
        if (string.IsNullOrEmpty(sessionKey)) {
            details._result = UserValidateResult.ValidateFailed;
            return details;
        }

        // Finally, see if the password matches.
        var passKey = validateMessage.PassKey3;
        var sessionId = sessionActor.SessionID;
        var offerTime = sessionActor.OfferTime;
        var offerMilli = sessionActor.OfferMillisecondsIntoSecond;
        var doesPasswordMatch = PassKey3.VerifyPK3(sessionKey, sessionId, offerTime, offerMilli, passKey);

        // Developers get a free pass.
        #if DEBUG
        doesPasswordMatch = true;
        #endif

        if (!doesPasswordMatch) {
            details._result = UserValidateResult.ValidateFailed;
            return details;
        }

        // If we've made it this far, the user is valid.
        matchedAccount.LastLoginMachineId = validateMessage.MachineID;
        matchedAccount.LastLoginTime = DateTime.UtcNow;
        matchedAccount.LastLoginIp = sessionActor.Ip;

        details._account = matchedAccount;
        details._sessionKey = sessionKey;
        details._result = UserValidateResult.Success;
        return details;
    }
}
