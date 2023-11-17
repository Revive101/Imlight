using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Game.Commands;
using System.Reflection;
using System.Text;

namespace Imlight.CoreLib.Game.Commands;

internal abstract class CommandProtocol {
    internal abstract string Group { get; set; }
    protected CommandContext Context;

    internal bool Execute(string commandName, CommandContext context, params object[] parameters) {
        this.Context = context;

        // Use reflection to find and execute the command method
        MethodInfo method = GetType()
            .GetMethod(commandName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (method == null) {
            Logger.Warning("Command {0} not found in protocol", Logger.Args(commandName));
            return false;
        }

        var authAttribute = method.GetCustomAttribute<AuthRequiredAttribute>();
        if (authAttribute != null) {
            if (!AuthorityRequester.RequestAuthority(authAttribute.Level, context.Account, $"Command {commandName}")) {
                InformSenderClient("You do not have permission to use this command.");
                return false;
            }
        }

        // If the parameter count doesn't match, return.
        var parameterCount = method.GetParameters().Length;
        if (parameterCount != parameters.Length) {
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
            return true;
        }

        var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
        if (commandAttribute != null) {
            method.Invoke(this, parameters);
        }
        else {
            Logger.Warning("Method {0} in command protocol {1} does not have the [2] attribute",
                Logger.Args(commandName, this.GetType(), nameof(commandAttribute)));
        }

        return true;
    }

    protected void InformSenderClient(string reason)
        => Context.SessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = reason });
}
