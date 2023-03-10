using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using Imlight.Net.Messages;
using Imlight.Common;

namespace Imlight.Login.Services
{
    internal class GameTransitionService : MessageService
    {
        public GameTransitionService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new GameTransitionService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER))]
        private void ReceiveSelectCharacter(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER message)
        {
            // If the socket account cannot be found, send the client an error.
            var account = GetSocketAccount();
            if (account is null)
            {
                Log.Logger.Error($"Service [{this.GetType()}] socket account could not be retreived!");

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() { Error = 1 });

                return;
            }

            // If the given character does not exist on this account, send the client an error.
            if (!account.GetCharacter(message.CharID, out var character))
            {
                Log.Logger.Warning($"Account [{account.ID}] attempted to get a character it didn't have.");

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() { Error = 1 });

                return;
            }

            // Otherwise, we've validated everything propertly. Retreive character
            // specific information and send the client the `MSG_CHARACTERSELECTED`.
            // This begins client transition to the game server.
            var charSelectedMsg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
            {
                IP = "127.0.0.1",                     // @FIXME: This should be sourced from elsewhere.
                TCPPort = 12600,                      // @FIXME: This should be sourced from elsewhere.
                UDPPort = 12600,                      // @FIXME: This should be sourced from elsewhere.
                Key = new ByteString(),               // Login server -> game server session key.
                UserID = account.ID,
                CharID = character.ID,
                ZoneID = new GID(123004564835992122), // Client doesn't actually use this ID.
                ZoneName = "WizardCity/WC_Ravenwood", // Client uses this name to load a zonelocally.
                Location = "Start",                   // Most zones use "Start" on player login.
                PrepPhase = 0,                        // (0|1): Player is in queue
                Slot = 0,                             // The player's position in said queue
                Error = 0,                            // String ID of an error code.
                LoginServer = "Imlight.Login"         // @FIXME: This should be sourced from elsewhere.
            };

            SendToSocket(charSelectedMsg);
        }

        private Account GetSocketAccount()
        {
            // Get the account from the AccountService.
            var internalMessage = new INTERN_ACCOUNT_PROTOCOL.INTMSG_GET_ACCOUNT();
            var account = AskInternal<INTERN_ACCOUNT_PROTOCOL.INTMSG_ACCOUNT>(internalMessage).Account;

            if (account is null)
            {
                Log.Logger.Error($"{this.GetType()} could not get account from AccountService.");
            }

            return account;
        }
    }
}
