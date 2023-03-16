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
using Imlight.Game;

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
                SendErrorToSocket();
                return;
            }

            // If the given character does not exist on this account, send the client an error.
            if (!account.GetCharacter(message.CharID, out var character))
            {
                Log.Logger.Warning($"Account [{account.ID}] attempted to get a character it didn't have.");
                SendErrorToSocket();
                return;
            }

            // Enqueue the session actor onto the game server and create a session key.
            var gameServer = GetGameServer();
            var serverEnqueueResult = (LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED)SessionActor.EnqueueToServer(gameServer.ActorRef);
            var allocatedKey = CreateSessionKey(gameServer.ActorRef, account);
            
            // Craft a successful message. This will instead be cached if the server is full.
            var charSelectedMsg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
            {
                // Set details about the game server.
                IP = gameServer.IP,
                TCPPort = gameServer.Port,
                UDPPort = gameServer.Port,
                Key = allocatedKey,                   // Login server -> game server session key.
                PrepPhase = 0,                        // (0|1): Player is in queue.
                Slot = 0,                             // The player's position in said queue.
                LoginServer = "Imlight.Login",        // @FIXME: This should be sourced from elsewhere.
                
                // Set details about the character.
                UserID = account.ID,
                CharID = character.ID,
                ZoneID = new GID((ulong)gameServer.Port),
                ZoneName = "WizardCity/WC_Hub", // Client uses this name to load a zone locally.
                Location = "Start",                   // Most zones use "Start" on player login.
            };
            
            // Cache the message if the player is queued.
            if (serverEnqueueResult.PrepPhase > 0)
            {
                SessionActor.CachedDequeueMessage = charSelectedMsg;
                SendToSocket(serverEnqueueResult);
            }
            else
            {
                SendToSocket(charSelectedMsg);
            }
        }

        private Account GetSocketAccount()
        {
            // Get the account from the AccountService.
            var internalMessage = new ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT();
            var account = AskInternal<ACCOUNT_104_PROTOCOL.MSG_ACCOUNT>(internalMessage).Account;

            if (account is null)
            {
                Log.Logger.Error($"{this.GetType()} could not get account from AccountService.");
            }

            return account;
        }

        private SERVER_100_PROTOCOL.MSG_SERVERINFO GetGameServer()
        {
            var msg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS();
            return AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg);
        }

        private ByteString CreateSessionKey(ICanTell gameServerRef, Account account)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_CREATEKEY()
            {
                Account = account
            };

            return gameServerRef.Ask<SERVER_100_PROTOCOL.MSG_CREATEKEYRSP>(msg)
                .Result
                .Key;
        }

        private void SendErrorToSocket(int errorCode = 1)
        {
            var msg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() { Error = errorCode };
            SendToSocket(msg);
        }
    }
}
