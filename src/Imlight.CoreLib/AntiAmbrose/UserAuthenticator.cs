/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * USER AUTHENTICATION
 * ========================================================================
 *
 * PURPOSE:
 * Provides an authentication mechnism for user login. Includes
 * checks for account existence, bans, and credential verification.
 *
 * USAGE EXAMPLE:
 * AuthenticationDetails authResult = UserAuthenticator.Authenticate(sessionActor, authMessage);
 * if (authResult._result == UserAuthenResult.Success) { ... }
 *
 * NOTE:
 * - 
 *
 * TODO:
 * - Last login time, machine ID, and IP should be saved to the database.
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Date: 3/19/2025
 */

using System;
using Imcodec.IO;
using Imlight.Common;
using Imlight.CoreLib.Shared.Cryptography;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imcodec.MessageLayer.Generated.LOGIN_7_PROTOCOL;

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
/// Manages user authentication during the login process.
/// </summary>
/// <remarks>
/// Differentiates from <see cref="UserValidator"/> by handling initial login authentication.
/// Performs checks for account existence, bans, and credential verification.
/// </remarks>
internal static class UserAuthenticator {

    private static readonly bool s_enforceRevision 
        = ConfigurationManager.Settings["Global Settings.EnforceRevision"].AsBool();
    private static readonly string s_serverRevision 
        = ConfigurationManager.Settings["Global Settings.GameRevision"].AsString();

    internal class AuthenticationDetails {
        
        internal Account _account;
        internal ByteString _sessionKey;
        internal ByteString _rec1;
        internal UserAuthenResult _result;

    }

    /// <summary>
    /// Authenticates the user using the provided <see cref="SessionActor"/> and <see cref="MSG_USER_AUTHEN_V3"/> message.
    /// </summary>
    /// <param name="sessionActor">The session actor.</param>
    /// <param name="authMessage">The authentication message.</param>
    /// <returns>The authentication details.</returns>
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

        // If the revision is enforced, check if the client revision matches the server revision.
        if (s_enforceRevision) {
            var clientRevision = authMessage.Revision;
            if (clientRevision != s_serverRevision) {
                Logger.Warning("SessionActor {0} was rejected due to revision mismatch. (Given: {1}, Expected: {2})",
                    Logger.Args(sessionActor.SessionID, clientRevision, s_serverRevision));

                details._result = UserAuthenResult.ErrorNoLock;

                return details;
            }
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

        // Check to see if the IP is banned.
        if (InfractionCollection.IsIpBanned(sessionActor.Ip)) {
            // Add an infraction to the account.
            matchedAccount.AddInfraction(InfractionType.Warn, "Logged in with banned IP.", null);
            details._result = UserAuthenResult.MachineBanned;

            return details;
        }

        // Check to see if this account is currently banned.
        if (matchedAccount.InfractionHistory.IsCurrentlyBanned || matchedAccount.IsLocked) {
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

            // todo: these are only ever cached and not ever persistently saved
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
        var decoded = Rec1.Decode(
            rec1, 
            sessionActor.SessionID, 
            sessionActor.OfferTime,
            sessionActor.OfferMillisecondsIntoSecond
        );
        var split = decoded.ToString().Split(' ');

        // The split should be of 3 parts: session id, username, and client key.
        if (split.Length != 3) {
            var exceptionMessage = "Failed to decode rec1. " +
                                   $"Expected 3 parts, got {split.Length}";
            throw new Exception(exceptionMessage);
        }

        // Cast the session id to a ushort.
        if (!ushort.TryParse(split[0], out var sId)) {
            var exceptionMessage = "Failed to decode rec1. " +
                                   $"Expected ushort, got {split[0]}";
            throw new Exception(exceptionMessage);
        }

        return (sId, split[1], split[2]);
    }
    
}
