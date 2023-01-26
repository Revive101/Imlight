using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using System.Net.Sockets;
using Imlight.Realm.Messages;
using WizUnraveler.DML;
using WizUnraveler.Cache;

namespace Imlight.Realm
{
    public class RealmActor : ReceiveActor
    {
        public string Name { get; init; }
        public sbyte ID { get; init; }

        private TcpServer _server;
        private Dictionary<ushort, IActorRef> _communicationActors;

        public RealmActor(string Name, sbyte ID)
        {
            this.Name = Name;
            this.ID = ID;
            this._server = new TcpServer(Self, TcpServer.DEFAULT_PORT);
            this._communicationActors = new Dictionary<ushort, IActorRef>();

            CreateReceiveHandlers();

            Log.Logger.Information($"RealmActor [{Name}] with ID [{ID}] created.");
        }

        public static Props Props(string Name, sbyte ID)
        {
            return Akka.Actor.Props.Create(() => new RealmActor(Name, ID));
        }

        private void CreateReceiveHandlers()
        {
            // Sent from the TcpServer to register a new socket connection.
            // Create a new socket communication actor.
            Receive<RegisterCommunicationActor>(x => RegisterCommunicationActor(x));

            // Respond to generic ping messages.
            Receive<SYSTEM_1_PROTOCOL.MSG_PING>(x => 
            {
                Sender.Tell(new SYSTEM_1_PROTOCOL.MSG_PING_RSP());
            });

            Receive<Terminated>(t => Log.Logger.Debug($"Actor [{t.ActorRef.Path}] terminated."));
        }

        private void RegisterCommunicationActor(RegisterCommunicationActor message)
        {
            var id = GetRandomID();
            var actorProps = CommunicationActor.Props(message.Socket, id, Self);
            var actor = Context.ActorOf(actorProps, id.ToString());
            _communicationActors.Add(id, actor);

            Context.Watch(actor);
        }

        private ushort GetRandomID()
        {
            var rand = new Random();
            while (true)
            {
                var temp = rand.Next(1, ushort.MaxValue);
                if (!_communicationActors.Keys.Any(x => x == temp))
                {
                    return (ushort)temp;
                }
                else continue;
            }
        }
    }
}
