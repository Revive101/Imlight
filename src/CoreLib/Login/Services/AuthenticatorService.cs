/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Login.Services;

internal class AuthenticatorService : MessageService {
    public AuthenticatorService(SessionActor parentActor) : base(parentActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new AuthenticatorService(parentActor));
    }

    #region Handlers

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
        }
    }

    #endregion

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
        }
    }
}
