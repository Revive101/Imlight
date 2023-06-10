using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WizUnraveler.Cache;

namespace Imlight.Server.Game.Services
{
    public class SpellService : MessageService
    {
        public SpellService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new SpellService(parentActor));
        }

        [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK))]
        private void ReceiveAddSpellToDeck(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK message)
        {
            Log.Logger.Debug("SpellID: "+ message.SpellID+ ", DeckID: "+message.DeckID+", Success: "+message.Success);
        }

    }
}
