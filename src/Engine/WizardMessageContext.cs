using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    public class WizardMessageContext
    {

        KIPacket Packet;
        sbyte RealmID;
        short SocketID;

        public WizardMessageContext(KIPacket packet, sbyte realmID, short socketID)
        {
            Packet = packet;
            RealmID = realmID;
            SocketID = socketID;
        }

        public WizardMessageContext(DataStreamContext context)
        {
            // Verify that the DataStreamContext stream is a KIPacket.
            if (!MessageFactory.IsKIPacket(context.Stream)) throw new ArgumentException("Context stream must be a valid KI packet stream!");

            this.Packet = MessageFactory.CreateKIPacketFromStream(context.Stream);
            this.RealmID = context.RealmID;
            this.SocketID = context.SocketID;
        }

        public static explicit operator WizardMessageContext(DataStreamContext context)
        {
            // Verify that the DataStreamContext stream is a KIPacket.
            if (!MessageFactory.IsKIPacket(context.Stream)) throw new ArgumentException("Context stream must be a valid KI packet stream!");

            return new WizardMessageContext(context);
        }

    }
}
