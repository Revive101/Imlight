/*
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * COMMAND DISPATCHER
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a command handling framework for testing game systems by dispatching
 * commands to appropriate protocols based on group names.
 * 
 * USAGE EXAMPLE:
 * CommandDispatcher.Instance.Tell(new SERVER_100_PROTOCOL.MSG_COMMAND() { 
 *     CommandText = "group command param1 param2",
 *     ActorRef = sender
 * });
 * 
 * NOTE:
 * Uses System.Reflection to dynamically discover and instantiate command protocols.
 * This class is intended for QA testing purposes only, not for production features.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;

namespace Imlight.CoreLib.Game.Commands;

/// <summary>
/// Dispatches game commands to registered protocol handlers based on group prefixes.
/// </summary>
/// <remarks>
/// Uses reflection to automatically discover and register command protocols.
/// Commands follow the format: [group] [command] [parameters].
/// For protocols without a group, the command itself is used as the lookup key.
/// </remarks>
internal class CommandDispatcher : ReceiveProtocolDispatcher {

    private const string GrouplessCommandPrefix = "nogroup";

    public static IActorRef Instance { get; private set; }

    private static Dictionary<string, CommandProtocol> s_protocols;

    public CommandDispatcher() {
        Instance = Self;
        s_protocols = [];

        // Get all types.
        var types = Assembly.GetExecutingAssembly().GetTypes();
        var keyIncrememnt = 0;
        foreach (var type in types) {
            if (type.IsSubclassOf(typeof(CommandProtocol))) {
                // Create an instance of the protocol and register it
                if (Activator.CreateInstance(type) is CommandProtocol protocol) {
                    // There may be duplicate keys here if the protocol doesn't have a group.
                    // Since such a protocol can arise from multiple assemblies, we'll just increment the key.
                    var groupName = protocol.Group.ToLower();
                    if (groupName is "" or null) {
                        groupName = $"{GrouplessCommandPrefix}{keyIncrememnt}";
                        keyIncrememnt++;
                    }

                    s_protocols[groupName] = protocol;
                }
            }
        }
    }

    public static Props Props() 
        => Akka.Actor.Props.Create(() => new CommandDispatcher());

    private void ExecuteCommand(string commandName, CommandContext context) {
        commandName = commandName.Trim();

        // The group name will be the first word in the command.
        var split = commandName.Split(' ');

        if (split.Length < 1) {
            InformSenderClient(context, "Command not found.");
            return;
        }

        var protocolName = split[0].ToLower();
        var command = split.Length > 1 ? split[1].ToLower() : "";

        if (s_protocols.TryGetValue(protocolName, out var protocol)) {
            // Create new parameters with the first two words removed.
            var parameters = split.Skip(2).ToArray();
            var executed = protocol.Execute(command, context, parameters);
            if (!executed) {
                InformSenderClient(context, "Command not found.");
            }
        }
        else {
            // If we couldn't find it, try searching for protocols with no group name.
            var protocols = s_protocols.Values.Where(x => string.IsNullOrEmpty(x.Group));
            var commandFound = false;

            foreach (var p in protocols) {
                var parameters = split.Skip(1).ToArray();
                commandFound = p.Execute(protocolName, context, parameters);

                if (commandFound) {
                    break; // Exit the loop once a matching command is found.
                }
            }

            if (!commandFound) {
                // Inform the invoker of his failure. Take his lunch money.
                InformSenderClient(context, "Command not found.");
            }
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        // Setup context before parsing any commands.
        var receiverContext = message.ActorRef;
        var characterContext = message.Wizard;
        var accountContext = message.Account;
        var objectContext = message.CoreObject;
        var context = new CommandContext() {
            SessionActor = receiverContext,
            CharacterObject = objectContext,
            Character = characterContext,
            Account = accountContext,
            ZoneActor = message.ZoneActor,
            ServerActor = message.ServerActor,
            SelectedCharacter = message.SelectedWizard,
            SelectedAccount = message.SelectedAccount
        };

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

        try {
            ExecuteCommand(message.CommandText, context);
        }
        catch (Exception ex) {
            // Log the exception and inform the client.
            // Choose which exception to use. If there's an inner exception, use that.
            var exception = ex.InnerException ?? ex;
            Logger.Error("Command dispatcher threw exception running command. Exception: {0} {1}",
                Logger.Args(exception.Message, exception.StackTrace));

            // Inform the client of the error. We'll want to prettify the exception message and stack trace.
            var prettyTrace = PrettifyStackTrace(exception.StackTrace);

            InformSenderClientImportant(context,
                                        $"An error occurred while executing the command. " +
                                        $"Exception: {exception.Message}<br><br> {prettyTrace}");
        }
    }

    private void InformSenderClient(CommandContext context, string reason)
        => context.SessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE() { Message = reason });

    private void InformSenderClientImportant(CommandContext context, string reason)
        => context.SessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE() { Message = reason, Modal = 1 });

    private static string PrettifyStackTrace(string stackTrace) {
        // "   at Imlight.CoreLib.WizardData.Models.Player.Wizard.AddItemToInventory(UInt64 itemId, WizClientObjectItem& item)
        // "   in /home/jay/Projects/Imlight/src/CoreLib/WizardData/Models/Player/Wizard.cs:line 163"
        var lines = stackTrace.Split('\n');
        var narrowedTrace = lines[0].TrimStart();

        // Return the narrowed trace.
        return narrowedTrace;
    }

}
