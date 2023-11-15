using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.IO;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using System;
using static Imlight.Common.Caches.LOGIN_7_PROTOCOL;

namespace Imlight.CoreLib.AntiAmbrose;

internal enum UserAuthenResult {
    Success = 0,
    AccountBanned = 0x538FBC0,
    MachineBanned = 0x44FB7BF8,
    AuthenFailed = 0x3B689180,
    AISNoLogin = 0x6311BDD6,
    Timeout = 0x512C42FF,
    FtpCapped = 0x5BFF7366,
    ErrorNoLock = 0x67DD13EA,
    FailedUpload = 0x10857D75
}

/// <summary>
/// This static class is responsible for authenticating users. Authentication happens during the login process
/// as opposed to <see cref="UserValidator"/>, which happens when a client is already logged in and is starting the game.
/// </summary>
internal static class UserAuthenticator {
    internal class AuthenticationDetails {
        internal Account _account;
        internal string _sessionKey;
        internal string _rec1;
        internal UserAuthenResult _result;
    }

    internal static AuthenticationDetails Authenticate(SessionActor sessionActor, MSG_USER_AUTHEN_V3 authMessage) {
        var sessionId  = sessionActor.SessionID;
        var offerTime  = sessionActor.OfferTime;
        var offerMilli = sessionActor.OfferMillisecondsIntoSecond;
        var (returnedSessionid, username, clientKey1) = DecodeRec1(authMessage.Rec1, sessionActor);
        var details = new AuthenticationDetails();

        // Check if the session id matches.
        if (returnedSessionid != sessionId) {
            details._result = UserAuthenResult.AuthenFailed;
            return details;
        }

        // Check if we can find the account.
        var matchedAccount = AccountCollection.GetAccount(username);
        if (matchedAccount is null) {
            details._result = UserAuthenResult.AuthenFailed;
            return details;
        }

        // Check to see if this machine is banned.
        if (InfractionCollection.IsMachineBanned(authMessage.MachineID)) {
            // Add an infraction to the account.
            matchedAccount.AddInfraction(InfractionType.Warn, "Logged in with banned machine ID.", null);

            details._result = UserAuthenResult.MachineBanned;
            return details;
        }

        // Check to see if this account is currently banned.
        if (matchedAccount.InfractionHistory.IsCurrentlyBanned) {
            details._result = UserAuthenResult.AccountBanned;
            return details;
        }

        details._account = matchedAccount;

        var doesPasswordMatch = ClientKey.VerifyCK1(matchedAccount.PasswordHash, sessionId, offerTime, offerMilli, clientKey1);
        if (doesPasswordMatch) {
            // Create a new session key and store it in the database.
            var sessionKey = ClientKey.HashSessionKey(sessionId, offerTime, offerMilli);
            ClientKeyCollection.AddSessionKey(matchedAccount.AccountId, authMessage.MachineID, sessionKey);
            details._sessionKey = sessionKey;

            matchedAccount.LastLoginMachineId = authMessage.MachineID;
            matchedAccount.LastLoginTime = DateTime.UtcNow;
            matchedAccount.LastLoginIp = sessionActor.Ip;

            // Craft a successful reply and return.
            var rec1 = Rec1.Encode(sessionKey, sessionId, offerTime, offerMilli);
            details._rec1 = rec1;
            return details;
        }
        else {
            details._result = UserAuthenResult.AuthenFailed;
            return details;
        }
    }

    private static (ushort, string, string) DecodeRec1(ByteString rec1, SessionActor sessionActor) {
        var decoded = Rec1.Decode(rec1, sessionActor.SessionID, sessionActor.OfferTime,
            sessionActor.OfferMillisecondsIntoSecond);
        var split = decoded.ToString().Split(' ');

        // Cast the session id to a ushort.
        if (!ushort.TryParse(split[0], out var sId)) {
            throw new Exception($"{nameof(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3)} Session ID is not a ushort. " +
                                $"Expected ushort, got {split[0]}");
        }

        return (sId, split[1], split[2]);
    }
}
