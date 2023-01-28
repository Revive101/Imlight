using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using WizUnraveler.Cache;

namespace Imlight.Realm
{
    public class RealmManagerActor : ServerReceiverActor 
    {
        public RealmManagerActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) { }

        public static Props Props(string Name, sbyte ID, ushort port)
        {
            return Akka.Actor.Props.Create(() => new RealmManagerActor(Name, ID, port));
        }

        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();

            // Respond to generic ping messages.
            Receive<SYSTEM_1_PROTOCOL.MSG_PING>(x => 
            {
                Sender.Tell(new SYSTEM_1_PROTOCOL.MSG_PING_RSP());
            });

            Receive<Terminated>(t => Log.Logger.Debug($"Actor [{t.ActorRef.Path}] terminated."));
        }
    }
}
