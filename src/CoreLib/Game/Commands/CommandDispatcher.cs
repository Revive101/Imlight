/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    private static IActorRef _instance;
    public static IActorRef Instance => _instance;
    private static readonly string s_gmIslandZoneName = "Housing/CardPromo/GS_Fantasy_Castle";
    private static readonly string[] s_gmIslandShortcutNames = new [] {
        "gm", "gmisland", "gm_island", "gmis", "gm_is", "gm_isl", "gm_isla", "gm_islan", "gm_island"
    };

    private IActorRef _receiverContext;
    private Account _accountContext;
    private Character _characterContext;
    private CoreObject _characterObjectContext;
    private readonly Dictionary<string, MethodInfo> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandDispatcher() {
        _instance = Self;

        var methods = this.GetType()
                          .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods) {
            var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null) {
                _commands[commandAttribute.Name.ToLower()] = method;

                var aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
                if (aliasAttribute is not null) {
                    foreach (var alias in aliasAttribute.Aliases) {
                        _commands[alias.ToLower()] = method;
                    }
                }
            }
        }
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new CommandDispatcher());

    private void ExecuteCommand(string commandName, params object[] parameters) {
        commandName = commandName.ToLower();
        if (!_commands.TryGetValue(commandName, out var method)) {
            Logger.Warning("Command not found: {0}", Logger.Args(commandName));

            // Inform the invoker of his failure. Take his lunch money.
            InformSenderClient("Command not found.");

            return;
        }

        var authAttribute = method.GetCustomAttribute<AuthRequiredAttribute>();
        if (authAttribute != null) {
            if (!AuthorityRequester.RequestAuthority(authAttribute.Level, _accountContext, $"Command {commandName}")) {
                InformSenderClient("You do not have permission to use this command.");
                return;
            }
        }

        // If the parameter count doesn't match, return.
        var parameterCount = method.GetParameters().Length;
        if (parameterCount != parameters.Length) {
            Logger.Warning("Command {0} requires {1} parameters but {2} were given",
                           Logger.Args(commandName, parameterCount, parameters.Length));

            // Write the usage of this command.
            var properUsageStr = new StringBuilder();
            properUsageStr.Append(commandName);
            properUsageStr.Append("<color;1C1EC4>");
            foreach (var parameter in method.GetParameters()) {
                properUsageStr.Append(" (");
                properUsageStr.Append(parameter.Name);
                properUsageStr.Append(')');
            }

            // Inform the invoker of improper usage. Point and laugh!
            InformSenderClient($"Proper usage: {properUsageStr}");
            return;
        }

        // Invoke the method.
        method.Invoke(this, parameters);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        // Setup context before parsing any commands.
        _receiverContext = message.ActorRef;
        _characterContext = message.PlayerCharacter;
        _accountContext = message.Account;
        _characterObjectContext = message.CoreObject;

        Logger.Information("{0} Uses command: {1}", Logger.Args(_accountContext.Username, message.CommandText));

        // Log the use of this command to the database.
        var chatLog = new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = _characterContext.Zone,
            CharacterId = _characterContext.CharId,
            AccountId = _accountContext.AccountId,
            Message = message.CommandText.ToString(),
        };
        CommandLogCollection.AddCommandLog(chatLog);

        var parameters = message.CommandText.ToString().Split(' ');
        var command = parameters[0].ToLower();
        var arguments = parameters.Skip(1).ToArray();

        ExecuteCommand(command, arguments);
    }

    [Command("teleport")]
    [Alias("tp", "port")]
    [AuthRequired(AuthLevel.Developer)]
    private void Teleport(string zone) {
        var actualZoneName = zone;
        var hasZone = AccessPassManager.DoesZoneExist(zone);
        if (!hasZone && !s_gmIslandShortcutNames.Any(x => x == zone)) {
            // Fallback to the zone name that is contained in the zone name.
            actualZoneName = AccessPassManager.GetContainedZoneName(zone);

            if (actualZoneName == null | actualZoneName == "") {
                Logger.Warning("Teleport command was given an invalid zone name {0}", Logger.Args(zone));
                InformSenderClient($"Zone {zone} does not exist.");
                return;
            }
        }
        else if (s_gmIslandShortcutNames.Any(x => x == zone)) {
            actualZoneName = s_gmIslandZoneName;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = actualZoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        _receiverContext.Tell(msg);
    }

    private void InformSenderClient(string reason)
        => _receiverContext.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE() {Message = reason});
}
