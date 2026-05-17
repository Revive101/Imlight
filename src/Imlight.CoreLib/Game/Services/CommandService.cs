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
 * COMMAND SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages in-game command processing and player selection mechanisms 
 * for administrative and interactive game functions.
 * 
 * USAGE EXAMPLE:
 * Internal service handling command routing and player context 
 * management within the game server session.
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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class CommandService(SessionActor sessionActor) : MessageService(sessionActor) {

    private readonly IActorRef _dispatcherRef = CommandDispatcher.Instance;

    private Wizard _selectedWizard;
    private Account _selectedAccount;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CommandService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(GAME_5_PROTOCOL.MSG_COMMAND message) {
        var coreObject = GetActiveGameObject();
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();

        _dispatcherRef.Tell(new SERVER_100_PROTOCOL.MSG_COMMAND() {
            CommandText = message.Command,
            ActorRef = SessionActor.ActorRef,
            CoreObject = coreObject,
            Wizard = wizard,
            Account = account,
            ZoneActor = SessionActor.GetZoneActor(),
            ServerActor = SessionActor.ServerRef,
            SelectedWizard = _selectedWizard,
            SelectedAccount = _selectedAccount
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        // We only care about the ID sent here. It's the ID of the core object, but Imlight serialized
        // it using the character ID.
        var id = message.BuddyID;

        var persistentWizard = WizardCollection.GetCharacterUnloaded(id);
        if (persistentWizard is null) {
            return;
        }

        var account = AccountCollection.GetAccount(persistentWizard.AccountId);
        if (account is null) {
            return;
        }

        // Cache for our next command.
        _selectedWizard = persistentWizard;
        _selectedAccount = account;
    }

}
