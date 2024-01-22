/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class CommandService : MessageService {
    private readonly IActorRef _dispatcherRef;

    private Wizard _selectedWizard;
    private Account _selectedAccount;

    public CommandService(SessionActor sessionActor) : base(sessionActor) => _dispatcherRef = CommandDispatcher.Instance;

    protected static Props Props(SessionActor parentActor) => Akka.Actor.Props.Create(() => new CommandService(parentActor));

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

        var persistentWizard = WizardCollection.GetCharacter(id);
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
