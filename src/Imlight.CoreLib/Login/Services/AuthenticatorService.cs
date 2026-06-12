/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * AUTHENTICATION SERVICE 
 * ========================================================================
 * 
 * PURPOSE:
 * Handles user authentication and session validation requests for the login server.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Authentication is the first step, where the user provides their credentials (username and password).
 * Validation is the second step, where the server checks if the user is already logged in or if their session is valid.
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Login.Services;

internal class AuthenticatorService(SessionActor parentActor) : MessageService(parentActor) {
    
    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new AuthenticatorService(parentActor));

    // Received when a user is trying to authenticate.
    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3))]
    private void ReceiveUserAuth(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3 message) {
        try {
            AuthenticateUser(message);
        }
        catch (Exception ex) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP {
                Error = (int) UserAuthenResult.Timeout,
                Reason = ex.Message,
            });

            throw new SessionFatalException("User authentication failed.", ex);
        }
    }

    // Received when a user is trying to validate its session.
    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE))]
    private void ReceiveUserValidate(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message) {
        try {
            ValidateUser(message);
        }
        catch (Exception ex) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP {
                Error = (int) UserValidateResult.Timeout,
                Reason = ex.Message,
            });

            throw new SessionFatalException("User validation failed.", ex);
        }
    }

    private void AuthenticateUser(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3 message) {
        var authReply = UserAuthenticator.Authenticate(SessionActor, message);

        // If the authentication reply has a failure, inform the socket and return.
        if (authReply._result != UserAuthenResult.Success) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP {
                Error = (int) authReply._result,
                Reason = authReply._result.ToString(),
            });
            
            return;
        }
        else {
            // Otherwise, inform the socket that the authentication was successful.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP {
                Error = (int) UserAuthenResult.Success,
                Reason = "",
                UserID = authReply._account.AccountId,
                PayingUser = 1,
                TimeStamp = "",
                Rec1 = authReply._rec1,
                //Flags = (int) authReply._account.GetAccountFlags(),
            });
        }

        SendClientToLogin(authReply._account);
    }

    private void ValidateUser(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message) {
        var validationReply = UserValidator.Validate(SessionActor, message);
        if (validationReply._result != UserValidateResult.Success) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP {
                Error = (int) validationReply._result,
                Reason = validationReply._result.ToString(),
            });
        }

        // Inform the socket that they've been validated.
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP {
            Error = (int) UserValidateResult.Success,
            Reason = "",
            UserID = validationReply._account.AccountId,
            PayingUser = 1,
            //Flags = (int) validationReply._account.GetAccountFlags(),
        });

        SendClientToLogin(validationReply._account);
    }

    private void SendClientToLogin(Account account) {
        // Inform the SessionActor of the account.
        TellOtherServices(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT { Account = account });

        // Enqueue ourselves to the connected server. Inform the socket if its been placed into a queue and
        // what position it could potentially be in.
        var serverEnqueueResult = SessionActor.EnqueueToServer();
        if (serverEnqueueResult.Failed) {
            var clientResponse = new LOGIN_7_PROTOCOL.MSG_USER_ADMIT_IND {
                Status = 0,
            };
            SendToSocket(clientResponse);

            // Explicitly inform the socket about this.
            var clientExplicitFailedResponse = new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE {
                Message = "This user is currently logged in elsewhere."
            };
            SendToSocket(clientExplicitFailedResponse);

            CloseSession();
        }
        else {
            var clientResponse = new LOGIN_7_PROTOCOL.MSG_USER_ADMIT_IND {
                PositionInQueue = (uint) serverEnqueueResult.PositionInQueue,
                Status = serverEnqueueResult.Status,
            };
            SendToSocket(clientResponse);

            // Add the player to the online player collection.
            AddToOnlineCollection(account);
        }
    }

    private void AddToOnlineCollection(Account account) {
        // Add the player to the online player collection.
        var onlinePlayer = new OnlinePlayer {
            SessionId = SessionActor.SessionID,
            AccountId = account.AccountId,
            CurrentRealm = "LoginServer",
            ActorPath = SessionActor.ActorRef.Path.ToString(),
        };

        OnlinePlayerCollection.AddOnlinePlayer(onlinePlayer);
    }
    
}
