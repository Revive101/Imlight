using Akka.Actor;
using Imlight.Common;
using Imlight.Data;
using Imlight.Net.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net
{
    /// <summary>
    /// Representes a networked ReceiveActor with an active TcpServer. 
    /// Adds more functionality to connect child actors to an acount object.
    /// </summary>
    public class AuthenticatedServerReceiverActor : ServerReceiverActor
    {
        public ConcurrentDictionary<ActorPath, Account> ActorAccounts { get; init; }

        public AuthenticatedServerReceiverActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) 
        { 
            ActorAccounts = new ConcurrentDictionary<ActorPath, Account>();
        }

        protected override void ConfigureReceivers()
        {
            Receive<UnregisterCommunicationActor>(x => ReceiveUnregisterCommunicationActor(x));

            base.ConfigureReceivers();
        }

        protected override void ReceiveUnregisterCommunicationActor(UnregisterCommunicationActor message)
        {
            // If we didn't have an account in the first place, it doesn't really matter.
            if (message.ActorReference is not null)
                ActorAccounts.TryRemove(message.ActorReference.Path, out _);

            base.ReceiveUnregisterCommunicationActor(message);
        }

        protected bool TryAddAccount(ActorPath actorPath, Account account)
        {
            if (!ActorAccounts.TryAdd(actorPath, account))
            {
                Log.Logger.Error($"ServerReceiverActor [{Name}] could not add account!");
                return false;
            }

            return true;
        }

        protected bool TryRemoveAccount(ActorPath actorPath)
        {
            if (!ActorAccounts.Remove(actorPath, out _))
            {
                Log.Logger.Error($"ServerReceiverActor [{Name}] could not remove account!");
                return false;
            }

            return true;
        }

        protected bool TryGetAccount(ActorPath actorPath, out Account account)
        {
            account = null;
            if (!ActorAccounts.TryGetValue(actorPath, out var val))
            {
                Log.Logger.Error($"ServerReceiverActor [{Name}] could not get account by path [{actorPath}]!");

                return false;
            }

            account = val;
            return true;
        }

        protected bool HasAccount(ActorPath actorPath)
        {
            if (!ActorAccounts.TryGetValue(actorPath, out _))
            {
                return false;
            }

            return true;
        }
    }
}
