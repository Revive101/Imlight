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
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;
using Serilog;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    private static IActorRef _instance;
    public static IActorRef Instance => _instance;

    private IActorRef _senderContext;
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
        if (_commands.TryGetValue(commandName, out var method)) {
            var authAttribute = method.GetCustomAttribute<AuthRequiredAttribute>();
            var currentAuthLevel = _accountContext.AuthLevel;
            if (authAttribute != null) {
                if (currentAuthLevel < authAttribute.Level) {
                    Logger.Warning("Command {0} requires auth level {1} but player has auth level {2}",
                                   Logger.Args(commandName, authAttribute.Level, currentAuthLevel));

                    // If the player is not capable of performing any sort of commands, log an infraction.
                    if (currentAuthLevel == AuthLevel.None) {
                        _accountContext.AddInfraction(InfractionType.SuspiciousBehavior, "Attempted to use commands without auth level");
                    }

                    return;
                }
            }

            // If the parameter count doesn't match, return.
            var parameterCount = method.GetParameters().Length;
            if (parameterCount != parameters.Length) {
                Logger.Warning("Command {0} requires {1} parameters but {2} were given",
                               Logger.Args(commandName, parameterCount, parameters.Length));
                return;
            }

            method.Invoke(this, parameters);
        }
        else {
            Logger.Warning("Command not found: {0}", Logger.Args(commandName));
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        // Setup context before parsing any commands.
        _senderContext = message.ActorRef;
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
        if (!hasZone) {
            // Fallback to the zone name that is contained in the zone name.
            actualZoneName = AccessPassManager.GetContainedZoneName(zone);

            if (!AccessPassManager.DoesZoneExist(actualZoneName)) {
                Log.Warning("Teleport command was given an invalid zone name {0}", Logger.Args(zone));
                return;
            }
        }

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = actualZoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        _senderContext.Tell(msg);
    }

    [Command("editcharacter")]
    [AuthRequired(AuthLevel.Administrator)]
    private void Editcharacter() {
        // This CoreObject is 100% compressed. Client crashes if not.
        var coreObjectSerializer = new CoreObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.UseFlags | SerializerOptions.Behaviors.Compress)
            .OnPropertyMask(0);
        var serializedCharObj = coreObjectSerializer.Serialize(_characterObjectContext);

        // This client will crash if the character registry is not present.
        var serializer = new ObjectSerializer();
        var registry = new CharacterRegistry();
        var serializedRegistry = serializer.Serialize(registry);

        var msg = new GAME_5_PROTOCOL.MSG_CSREDITCHARACTER() {
            ChunkNum = 0,
            CharacterID = _characterContext.CharId,
            UserID = _accountContext.AccountId,
            UserName = _accountContext.Username,
            CurrentBan = "",  // Serialized?
            CurrentMute = "", // Serialized?
            Object = serializedCharObj,
            CurrentQuests = "",
            Registry = serializedRegistry,
            AccessPasses = "",
            BadgeList = "",
            Edit = 0,
            AllowedToReport = 0,
        };
        _senderContext.Tell(msg);
    }
}
