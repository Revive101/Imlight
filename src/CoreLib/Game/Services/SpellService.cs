using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

public class SpellService : MessageService {
    public SpellService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new SpellService(parentActor));
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK))]
    private void ReceiveAddSpellToDeck(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK message) {
        Logger.Debug("SpellID: " + message.SpellID + ", DeckID: " + message.DeckID + ", Success: " + message.Success);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = 1
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK))]
    private void ReceiveRemoveSpellFromDeck(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK message) {
        Logger.Debug("SpellID: " + message.SpellID + ", DeckID: " + message.DeckID + ", Success: " + message.Success);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = 1
        });
    }
}
