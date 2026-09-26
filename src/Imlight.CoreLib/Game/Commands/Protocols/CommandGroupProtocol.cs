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
 * GROUP SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Chat commands for inspecting and managing the caller's group. Regular
 * groups have no remove or disband button in the client, so these are the
 * way to exercise those paths.
 * 
 * USAGE EXAMPLE:
 * .group info
 * .group kick Some Wizard Name
 * .group leader Some Wizard Name
 * .group disband
 * 
 * NOTE:
 * kick, leader and disband act with the caller's own leader rights, the
 * same rules the client requests follow.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandGroup : CommandProtocol {

    internal override string Group { get; set; } = "group";

    [Command("info")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GroupInfoCommand() {
        if (!GroupRegistry.TryGetGroup(Context.Character.CharId, out var group)) {
            InformSenderClient("You are not in a group.");

            return;
        }

        var members = group.Members.Select(member => {
            var name = WizardCollection.GetCharacter(member.CharId)?.PlayerNameBehavior.GetWizardName() ?? $"{member.CharId}";
            var leader = member.CharId == group.LeaderCharId ? " (leader)" : string.Empty;
            var online = member.IsOnline ? member.ZonePath : "between zones";

            return $"{name}{leader}: {online}, slot {member.SigilSlot}";
        });

        InformSenderClient($"Group {group.GroupId}: {string.Join("; ", members)}");
    }

    [Command("kick")]
    [Alias("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GroupKickCommand([Remainder] string memberName) {
        if (!TryFindMember(memberName, out var memberCharId)) {
            return;
        }

        GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_REMOVEMEMBER {
            RequesterCharId = Context.Character.CharId,
            TargetCharId = memberCharId
        });
    }

    [Command("leader")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GroupLeaderCommand([Remainder] string memberName) {
        if (!TryFindMember(memberName, out var memberCharId)) {
            return;
        }

        GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_CHANGELEADER {
            RequesterCharId = Context.Character.CharId,
            NewLeaderCharId = memberCharId
        });
    }

    [Command("disband")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void GroupDisbandCommand()
        => GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_DISBANDGROUP {
            RequesterCharId = Context.Character.CharId
        });

    private bool TryFindMember(string memberName, out ulong memberCharId) {
        memberCharId = 0;

        if (!GroupRegistry.TryGetGroup(Context.Character.CharId, out var group)) {
            InformSenderClient("You are not in a group.");

            return false;
        }

        var match = group.Members.FirstOrDefault(member => string.Equals(
            WizardCollection.GetCharacter(member.CharId)?.PlayerNameBehavior.GetWizardName(),
            memberName.Trim(),
            StringComparison.OrdinalIgnoreCase));
        if (match is null) {
            InformSenderClient($"Nobody named {memberName} is in your group.");

            return false;
        }

        memberCharId = match.CharId;

        return true;
    }

}
