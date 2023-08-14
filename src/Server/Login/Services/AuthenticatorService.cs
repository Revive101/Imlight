/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common.Cryptography;
using Imlight.Server.Login.Exceptions;
using Imlight.Server.Login.Models;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using Imlight.Server.WizardData.Implementations;
using WizUnraveler.IO;

namespace Imlight.Server.Login.Services;

internal class AuthenticatorService : MessageService
{
    public AuthenticatorService(SessionActor parentActor) : base(parentActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new AuthenticatorService(parentActor));
    }
    
    #region Handlers

    // Received when a user is trying to authenticate.
    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3))]
    private void ReceiveUserAuth(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3 message)
    {
        try
        {
            AuthenticateUser(message);
        }
        catch (Exception ex)
        {
            SendAuthenFailed(UserAuthenError.Timeout, ex.Message);
        }
    }

    // Received when a user is trying to validate its session.
    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE))]
    private void ReceiveUserValidate(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message)
    {
        try
        {
            ValidateUser(message);
        }
        catch (Exception ex)
        {
            SendValidateFailed(UserValidateError.Timeout, ex.Message);
        }
    }

    #endregion
    
    private void AuthenticateUser(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3 message)
    {
        // Craft the record.
        var offerTime = SessionActor.OfferTime;
        var offerMilli = SessionActor.OfferMillisecondsIntoSecond;
        var (sId, username, ck1) = DecodeRec1(message.Rec1);

        // Check if the session id matches the one we sent. If it doesn't, inform the socket and return.
        if (sId != SessionActor.SessionID)
        {
            // Return an error to the client.
            SendAuthenFailed(UserAuthenError.AuthenFailed, "Session ID mismatch.");
            return;
        }
        
        // Get the account from database using the given user id. If the account doesn't exist, inform the socket
        // and return.
        var matchedAccount = AccountCollection.GetAccount(username);
        if (matchedAccount == null)
        {
            SendAuthenFailed(UserAuthenError.AuthenFailed, "Invalid UserID");
            return;
        }

        // Check if the password hash matches the one we sent. If it doesn't, inform the socket and return.
        var doesPassMatch = ClientKey.VerifyCK1(matchedAccount.PasswordHash, sId, offerTime, offerMilli, ck1);
        if (doesPassMatch)
        {
            // Create a session key and store it in the database.
            var sessionKey = ClientKey.HashSessionKey(sId, offerTime, offerMilli);
            ClientKeyCollection.AddSessionKey(matchedAccount.AccountId, message.MachineID, sessionKey);

            // Echo the session key and user id back to the client.
            var rec1 = Rec1.Encode(sessionKey, sId, offerTime, offerMilli);
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP()
            {
                Error = 0,
                Flags = 0,
                PayingUser = 1,
                Reason = "",
                Rec1 = rec1,
                TimeStamp = "",
                UserID = matchedAccount.AccountId
            });
        }
        else
        {
            SendAuthenFailed(UserAuthenError.AuthenFailed, "Invalid Password");
            return;
        }
        
        SendClientToLogin(matchedAccount);
    }

    private void ValidateUser(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message)
    {
        // Get the account from database using the given user id. If the account doesn't exist,
        // inform the socket and return.
        var matchedAccount = AccountCollection.GetAccount(message.UserID);
        if (matchedAccount == null)
        {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP()
            {
                UserID = message.UserID,
                PayingUser = 1,
                Error = (int)UserValidateError.ValidateFailed,
                Reason = "Invalid UserID",
            });
            return;
        }
        
        // Get the stored session key associated with the user id. If a session key doesn't exist, inform the socket
        // and return.
        var sessionKey = ClientKeyCollection.GetSessionKey(message.UserID, message.MachineID);
        if (string.IsNullOrEmpty(sessionKey))
        {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP()
            {
                UserID = message.UserID,
                PayingUser = 1,
                Error = (int)UserValidateError.ValidateFailed,
                Reason = "Invalid SessionKey",
            });
            return;
        }

        // The client has echoed the session key back to us. We can now verify the passkey.
        var ps3Raw = message.PassKey3;
        var sId = SessionActor.SessionID;
        var offerTime = SessionActor.OfferTime;
        var offerMilli = SessionActor.OfferMillisecondsIntoSecond;
        var passKey = PassKey3.VerifyPK3(sessionKey, sId, offerTime, offerMilli, ps3Raw);
        
        // If the passkey is invalid, inform the socket and return.
        if (!passKey)
        {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP
            {
                UserID = message.UserID,
                PayingUser = 1,
                Error = (int)UserValidateError.ValidateFailed,
                Reason = "Invalid PassKey3",
            });
            return;
        }

        // Otherwise, send the client to the login server.
        SendClientToLogin(matchedAccount);
            
        // Inform the player that they've been authenticated.
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP()
        {
            UserID = message.UserID,
            PayingUser = 1,
            Error = (int)UserValidateError.NoError,
            Reason = "", // Unclear as to what this field means, but it's most likely an elaboration of an error.
        });
    }
    
    private (ushort, string, string) DecodeRec1(ByteString rec1)
    {
        var decoded = Rec1.Decode(rec1, SessionActor.SessionID, SessionActor.OfferTime,
            SessionActor.OfferMillisecondsIntoSecond);
        var split = decoded.ToString().Split(' ');
        
        // Cast the session id to a ushort.
        if (!ushort.TryParse(split[0], out var sId))
        {
            throw new Exception($"{nameof(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3)} Session ID is not a ushort. " +
                                $"Expected ushort, got {split[0]}");
        }
        
        return (sId, split[1], split[2]);
    }

    private void SendAuthenFailed(UserAuthenError error, string reason)
    {
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP
        {
            Error = (int)error,
            Reason = reason
        });
    }

    private void SendValidateFailed(UserValidateError error, string reason)
    {
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP
        {
            Error = (int)error,
            Reason = reason
        });
    }
    
    private void SendClientToLogin(Account account)
    {
        // Inform the SessionActor of the account.
        TellOtherServices(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT { Account = account });

        // Enqueue ourselves to the connected server. Inform the socket if its been placed into a queue and
        // what position it could potentially be in.
        var serverEnqueueResult = (LOGIN_7_PROTOCOL.MSG_USER_ADMIT_IND)SessionActor.EnqueueToServer();
        SendToSocket(serverEnqueueResult);
    }
}