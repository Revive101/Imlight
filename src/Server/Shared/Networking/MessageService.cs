/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using WizUnraveler.DML;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.Game.Services;
using Imlight.Server.Login.Models;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;

namespace Imlight.Server.Shared.Networking
{
    public abstract class MessageService : ReceiveProtocolDispatcher
    {
        protected SessionActor SessionActor { get; init; }

        public MessageService(SessionActor sessionActor)
        {
            this.SessionActor = sessionActor;
        }

        /// <summary>
        /// Sends a message directly to the socket.
        /// </summary>
        /// <param name="message"></param>
        protected void SendToSocket(INetworkMessage message)
        {
            if (SessionActor is null)
                throw new ActorKilledException($"{GetType()} attempted to send message to undefined SessionActor.");

            SessionActor.ActorRef.Tell(message);
        }

        /// <summary>
        /// Sends the SessionActor a server message. Used to send data to another service of the SessionActor.
        /// </summary>
        /// <param name="message"></param>
        protected void TellOtherServices(IServerMessage message)
        {
            if (message.ServiceID < 100)
            {
                throw new Exception($"You are sending a non-server message using {nameof(TellOtherServices)}! " +
                                    $"Do not do this. Use {nameof(SendToSocket)} instead.");
            }

            SessionActor.ActorRef.Tell(message);
        }

        /// <summary>
        /// Broadcasts a message to the entire zone.
        /// </summary>
        /// <param name="originalMessage"></param>
        /// <param name="isSelfless"></param>
        /// <exception cref="ActorKilledException"></exception>
        protected void ZoneBroadcast(INetworkMessage originalMessage, bool isSelfless = true)
        {
            if (SessionActor is null)
                throw new ActorKilledException($"{GetType()} attempted to send message to undefined SessionActor.");

            var msg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST
            {
                Message = originalMessage,
                Selfless = isSelfless,
                Sender = SessionActor.ActorRef
            };
            
            SessionActor.ActorRef.Tell(msg);
        }

        /// <summary>
        /// Asks the SessionActor for a return. Used to get data from another service of the SessionActor.
        /// </summary>
        /// <param name="message"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T AskOtherService<T>(IServerMessage message)
            where T : IServerMessage
        {
            if (SessionActor is null)
            {
                throw new ActorKilledException($"{this.GetType()} attempted to send message to undefined SessionActor.");
            }
            
            if (message.ServiceID < 100)
            {
                throw new Exception($"You are sending a non-server message using {nameof(AskOtherService)}! " +
                                    $"Do not do this. Use {nameof(SendToSocket)} instead.");
            }

            var task = SessionActor.HandleInternalAsk<T>(message);

            return task;
        }

        /// <summary>
        /// Asks the server the SessionActor is connected to.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T AskServer<T>(IServerMessage message)
            where T : IServerMessage
        {
            if (SessionActor is null)
            {
                Log.Error("{0} attempted to send message to undefined SessionActor.", Log.Args(this.GetType()));
                return default(T);
            }
            if (message.ServiceID < 100)
            {
                throw new Exception($"You are sending a non-server message using {nameof(AskServer)}! " +
                                    $"Do not do this. Use {nameof(SendToSocket)} instead.");
            }

            var task = SessionActor.AskServer<T>(message);
            return task;
        }

        /// <summary>
        /// Gets the connected account attached to the current SessionActor. An AccountService must be
        /// attached to the SessionActor prior.
        /// </summary>
        /// <returns></returns>
        protected Account GetSocketAccount()
        {
            // Get the account from the AccountService.
            var internalMessage = new ACCOUNT_104_PROTOCOL.MSG_QUERYACCOUNT();
            var account = AskOtherService<ACCOUNT_104_PROTOCOL.MSG_ACCOUNT>(internalMessage).Account;

            if (account is null)
            {
                throw new Exception($"{GetType()} could not get account from AccountService.");
            }

            return account;
        }
        
        /// <summary>
        /// Sends the SessionActor a close message.
        /// </summary>
        protected void CloseSession()
        {
            SessionActor.ActorRef.Tell("Close");
        }
        
        /// <summary>
        /// Gets the active <see cref="TypeCache.CoreObject"/> of this session. Requires an active
        /// <see cref="CharacterService"/> as a running service.
        /// </summary>
        /// <returns></returns>
        protected TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }

        /// <summary>
        /// Gets the active <see cref="Character"/> of this session. Requires an active
        /// <see cref="CharacterService"/> as a running service.
        /// </summary>
        /// <returns></returns>
        protected Character GetActiveCharacter()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskOtherService<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);
            
            return response.Character;
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Log.Error("MessageService {ServiceName} restarting due to {Reason}", 
                Log.Args(GetType().Name, reason.Message));
            
            base.PreRestart(reason, message);
        }
        
        /// <summary>
        /// Called when the service is about to be disposed. It is guaranteed that other services are still running,
        /// so this method is used to gracefully shutdown any service who's disposal may affect other services.
        /// Ensure that you call the base method when overriding this method to return a graceful shutdown response.
        /// </summary>
        protected virtual void OnPreDispose()
        {
            Sender.Tell(new SERVICE_101_PROTOCOL.MSG_PREDISPOSE());
        }

        /// <summary>
        /// Called when the service is disposed. This is where you should clean up any resources.
        /// </summary>
        protected virtual void OnDispose()
        {
            GC.SuppressFinalize(this);
        }
        
        #region Handlers
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY))]
        public void ReceiveMessageServiceIdentify(SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY message)
        {
            var rsp = new SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY()
            {
                Service = this
            };
            
            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_PREDISPOSE))]
        public void ReceivePreDispose(SERVICE_101_PROTOCOL.MSG_PREDISPOSE message)
        {
            OnPreDispose();
        }
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE))]
        public void ReceiveDispose(SERVICE_101_PROTOCOL.MSG_DISPOSE message)
        {
            OnDispose();
        }
        
        #endregion
    }
}
