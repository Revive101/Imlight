/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands;

internal abstract class CommandProtocol {

    internal abstract string Group { get; set; }
    protected CommandContext Context;

    private Dictionary<string, MethodInfo> _commandMethods;
    private bool _hasInitiated;

    internal bool Execute(string commandName, CommandContext context, params object[] parameters) {
        // If we haven't initiated the commands, do so now.
        if (!_hasInitiated) {
            InitiateHandlers();
        }

        this.Context = context;

        if (commandName is "help" or "?" or "" or null && !string.IsNullOrEmpty(Group)) {
            InformClientHelp();
            return true;
        }

        if (!_commandMethods.TryGetValue(commandName.ToLower(), out var method)) {
            if (!string.IsNullOrEmpty(Group)) {
                // We don't need to log here if this is an ungrouped command. The command dispatcher is just
                // firing everywhere.
                Logger.Warning("Command {0} not found in {1}", Logger.Args(commandName, GetType().Name));
            }
            return false;
        }

        var authAttribute = method.GetCustomAttribute<AuthRequiredAttribute>();
        if (authAttribute != null) {
            var actualAuthLevel = authAttribute.Level;
#if !DEBUG
            // If the auth level is developer, bump it up to administrator if we're not in debug builds.
            if (actualAuthLevel == AuthLevel.Developer) {
                actualAuthLevel = AuthLevel.Administrator;
            }
#endif

            if (!AuthorityRequester.RequestAuthority(actualAuthLevel, context.Account, $"Command {commandName}")) {
                InformSenderClient("You do not have permission to use this command.");
                return true;
            }
        }

        // Process the parameters with respect to RemainderAttribute, if it is present.
        var methodParameters = method.GetParameters();
        var expectedParameterCount = methodParameters.Length;
        if (expectedParameterCount > 0) {
            // If we expected parameters, but none were provided, return.
            if (parameters.Length == 0) {
                InformClientOfProperParameterCount(commandName, methodParameters);
                return true;
            }

            var processedParameters = new List<object>();
            for (int i = 0; i < methodParameters.Length; i++) {
                if (methodParameters[i].GetCustomAttribute<RemainderAttribute>() != null) {
                    // Combine the remaining parameters into a single string
                    var restOfString = string.Join(" ", parameters.Skip(i));
                    processedParameters.Add(restOfString);

                    break;
                }
                else {
                    processedParameters.Add(parameters[i]);
                }
            }

            // If the parameter count doesn't match, return.
            if (expectedParameterCount != processedParameters.Count) {
                InformClientOfProperParameterCount(commandName, methodParameters);
                return true;
            }

            method.Invoke(this, [.. processedParameters]);
            return true;
        }
        else {
            // Invoke the method with no parameters.
            method.Invoke(this, null);
            return true;
        }
    }

    protected void InformSenderClient(string reason, bool isImportant = false)
        => Context.SessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE {
            Message = reason,
            Modal = (byte) (isImportant ? 1 : 0)
        });

    private void InitiateHandlers() {
        _hasInitiated = true;
        _commandMethods = new Dictionary<string, MethodInfo>();

        // Get all the methods in this class that have the Command attribute.
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var method in GetType().GetMethods(bindingFlags)) {
            var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null) {
                _commandMethods[commandAttribute.Name.ToLower()] = method;
            }

            // Add an entry for each of the aliases on the Alias attribute, if it has one.
            var aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
            if (aliasAttribute != null) {
                foreach (var alias in aliasAttribute.Aliases) {
                    _commandMethods[alias.ToLower()] = method;
                }
            }
        }
    }

    private void InformClientOfProperParameterCount(string commandName, ParameterInfo[] methodParameters) {
        // Write the usage of this command.
        var properUsageStr = new StringBuilder();
        properUsageStr.Append(commandName);
        properUsageStr.Append("<color;1C1EC4>");

        foreach (var parameter in methodParameters) {
            properUsageStr.Append(" (");
            properUsageStr.Append(parameter.Name);

            if (parameter.GetCustomAttribute<RemainderAttribute>() != null) {
                properUsageStr.Append("...");
            }

            properUsageStr.Append(')');
        }

        // Inform the invoker of improper usage. Point and laugh!
        InformSenderClient($"Proper usage: {properUsageStr}");
    }

    private void InformClientHelp() {
        var sb = new StringBuilder()
            .AppendLine("Available commands:");

        var seenCommands = new HashSet<MethodInfo>();
        foreach (var command in _commandMethods) {
            // If we've already seen this command, skip it.
            if (!seenCommands.Add(command.Value)) {
                continue;
            }

            var commandAttribute = command.Value.GetCustomAttribute<CommandAttribute>();

            // Get all the parameters for this command. Put a '$' in front of each parameter name.
            var parameterStr = string.Join(" ", command.Value.GetParameters().Select(x => $"${x.Name}"));

            sb.AppendLine($"{Group} {commandAttribute.Name} {parameterStr}");
        }

        InformSenderClient(sb.ToString(), true);
    }

}
