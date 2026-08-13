/*
 * Imlight
 * Copyright (C) 2026 Revive101
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
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandDynaMod : CommandProtocol {

    internal override string Group { get; set; } = "dynamod";

    [Command("list")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ListDynaModsCommand() {
        var dynamods = Context.Character.DynamodSet?.Dynamods ?? [];
        if (dynamods.Count == 0) {
            InformSenderClient("No dynamods on this character.");

            return;
        }

        var summary = string.Join(", ", dynamods
            .Where(d => d is not null)
            .Select(d => $"{d.ClientTag} = {d.ModState}"));
        InformSenderClient($"Dynamods: {summary}");
    }

    [Command("set")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void SetDynaModCommand(string state, [Remainder] string clientTag) {
        var normalizedState = state.ToLowerInvariant() switch {
            "on" => "On",
            "off" => "Off",
            _ => null
        };
        if (normalizedState is null) {
            InformSenderClient("Invalid state. Use 'on' or 'off'.");

            return;
        }

        SendDynaModAdd(clientTag, normalizedState);
        InformSenderClient($"Dynamod '{clientTag}' set to {normalizedState}.");
    }

    [Command("toggle")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void ToggleDynaModCommand([Remainder] string clientTag) {
        var currentState = Context.Character.DynamodSet?.Dynamods?
            .FirstOrDefault(d => d is not null
                && d.ClientTag.Equals(clientTag, StringComparison.OrdinalIgnoreCase))?
            .ModState;

        var newState = string.Equals(currentState, "Off", StringComparison.OrdinalIgnoreCase) ? "On" : "Off";

        SendDynaModAdd(clientTag, newState);
        InformSenderClient($"Dynamod '{clientTag}' toggled to {newState} (was {(currentState is null ? "not present" : currentState)}).");
    }

    [Command("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void RemoveDynaModCommand([Remainder] string clientTag) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_REMOVEDYNAMOD {
            DynaMod = new ResRemoveDynaMod {
                m_dynaModClientTag = clientTag
            },
            ContextActor = Context.SessionActor
        };
        Context.SessionActor.Tell(msg);

        InformSenderClient($"Dynamod '{clientTag}' removed.");
    }

    private void SendDynaModAdd(string clientTag, string state) {
        var msg = new CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD {
            DynaMod = new ResAddDynaMod {
                m_dynaModClientTag = clientTag,
                m_dynaModState = state,
                m_zoneName = ""
            },
            ContextActor = Context.SessionActor
        };
        Context.SessionActor.Tell(msg);
    }

}
