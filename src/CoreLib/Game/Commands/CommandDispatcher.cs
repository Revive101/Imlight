/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    public static IActorRef Instance { get; private set; }

    private static Dictionary<string, CommandProtocol> s_protocols;

    public CommandDispatcher() {
        Instance = Self;
        s_protocols = new Dictionary<string, CommandProtocol>();

        // Get all types in the same namespace as CommandDispatcher
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Namespace == typeof(CommandDispatcher).Namespace);

        foreach (var type in types) {
            if (type.IsSubclassOf(typeof(CommandProtocol))) {
                // Create an instance of the protocol and register it
                if (Activator.CreateInstance(type) is CommandProtocol protocol) {
                    s_protocols[protocol.Group.ToLower()] = protocol;
                }
            }
        }
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new CommandDispatcher());

    private void ExecuteCommand(string commandName, CommandContext context, params object[] parameters) {
        if (string.IsNullOrWhiteSpace(commandName)) {
            InformSenderClient(context, "Command name is null or empty.");
            return;
        }

        commandName = commandName.Trim().ToLower();

        if (commandName.Contains(" ")) {
            // The group name will be the first word in the command.
            var group = commandName.Split(' ')[0];
            if (s_protocols.TryGetValue(group, out var protocol)) {
                protocol.Execute(commandName, context, parameters);
            }
            else {
                InformSenderClient(context, "Command not found.");
            }
        }
        else {
            // Handle the case where commandName does not contain a space.
            InformSenderClient(context, "Command name does not contain a group.");
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        // Setup context before parsing any commands.
        var receiverContext = message.ActorRef;
        var characterContext = message.PlayerCharacter;
        var accountContext = message.Account;
        var objectContext = message.CoreObject;
        var context = new CommandContext(receiverContext, objectContext, characterContext, accountContext);

        Logger.Information("{0} Uses command: {1}", Logger.Args(accountContext.Username, message.CommandText));

        // Log the use of this command to the database.
        var chatLog = new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = characterContext.Zone,
            CharacterId = characterContext.CharId,
            AccountId = accountContext.AccountId,
            Message = message.CommandText.ToString(),
        };
        CommandLogCollection.AddCommandLog(chatLog);

        var parameters = message.CommandText.ToString().Split(' ');
        var command = parameters[0].ToLower();
        var arguments = parameters.Skip(1).ToArray();

        ExecuteCommand(command, context, arguments);
    }

    private void InformSenderClient(CommandContext context, string reason)
        => context.SessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE() {Message = reason});
}
