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
 * Thread-safe, read-only lookup of the current group snapshots. Only the
 * GroupDirectory writes here; everything else reads.
 * 
 * USAGE EXAMPLE:
 * GroupRegistry.IsGroupChannel(message.TargetID);
 * GroupRegistry.TryGetGroup(wizard.CharId, out var group);
 * 
 * NOTE:
 * Readers may observe a snapshot that is one change behind the directory.
 * Anything that must be exact (invites, leader checks) goes through the
 * directory actor instead.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Groups;

internal static class GroupRegistry {

    private static readonly ConcurrentDictionary<ulong, GroupSnapshot> s_groupsByMember = new();
    private static readonly ConcurrentDictionary<ulong, GroupSnapshot> s_groupsByChannel = new();

    public static bool TryGetGroup(ulong charId, out GroupSnapshot group)
        => s_groupsByMember.TryGetValue(charId, out group);

    public static bool IsGroupChannel(ulong channelId)
        => channelId != 0 && s_groupsByChannel.ContainsKey(channelId);

    public static bool AreGrouped(ulong firstCharId, ulong secondCharId)
        => TryGetGroup(firstCharId, out var group) && group.HasMember(secondCharId);

    internal static void Publish(GroupSnapshot group, IEnumerable<ulong> formerMemberIds) {
        foreach (var formerMemberId in formerMemberIds) {
            s_groupsByMember.TryRemove(formerMemberId, out _);
        }

        foreach (var member in group.Members) {
            s_groupsByMember[member.CharId] = group;
        }

        s_groupsByChannel[group.ChannelId] = group;
    }

    internal static void Withdraw(ulong channelId, IEnumerable<ulong> formerMemberIds) {
        foreach (var formerMemberId in formerMemberIds) {
            s_groupsByMember.TryRemove(formerMemberId, out _);
        }

        s_groupsByChannel.TryRemove(channelId, out _);
    }

}
