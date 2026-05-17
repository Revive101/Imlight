/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * SPELLBOOK SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player spell deck modifications, including adding and 
 * removing spells from spell decks.
 * 
 * USAGE EXAMPLE:
 * Internal service handling spellbook-related messages within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

internal class SpellbookService(SessionActor sessionActor) : MessageService(sessionActor) {

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new SpellbookService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK))]
    private void ReceiveAddSpellToDeck(WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK message) {
        var wizard = GetActiveWizard();
        var deckAddSuccess = wizard.AddSpellToDeck((uint) message.SpellID, message.DeckID);

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTODECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = (byte) (deckAddSuccess ? 1 : 0)
        });
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK))]
    private void ReceiveRemoveSpellFromDeck(WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK message) {
        var wizard = GetActiveWizard();
        var deckRemoveSuccess = wizard.RemoveSpellFromDeck((uint) message.SpellID, message.DeckID);

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMDECK() {
            SpellID = message.SpellID,
            DeckID = message.DeckID,
            Success = (byte) (deckRemoveSuccess ? 1 : 0)
        });
    }
    
}
