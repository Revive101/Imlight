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
 * The mutable state of one group. Owned and mutated only by the
 * GroupDirectory actor; other code reads GroupSnapshot instead.
 * 
 * USAGE EXAMPLE:
 * var group = new Group(groupId, channelId, leaderCharId);
 * group.AddMember(inviteeCharId);
 * 
 * NOTE:
 * A group exists as soon as its founder sends the first invite, but it is
 * not announced to anyone until a second member joins. Members keep their
 * join order; the earliest remaining member inherits leadership.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Groups;

internal sealed class Group(ulong groupId, ulong channelId, ulong founderCharId) {

    public ulong GroupId { get; } = groupId;
    public ulong ChannelId { get; } = channelId;
    public ulong LeaderCharId { get; set; } = founderCharId;
    public bool IsAnnounced { get; set; }
    public ulong QuestId { get; set; }
    public ulong GoalId { get; set; }
    public byte[] QuestData { get; set; }

    public List<ulong> MemberIds { get; } = [founderCharId];
    public Dictionary<ulong, uint> SigilSlots { get; } = [];
    public HashSet<ulong> PendingInviteeIds { get; } = [];

    public int Count => MemberIds.Count;

    public bool IsFull => MemberIds.Count >= GroupDirectory.MaxGroupSize;

    public bool HasMember(ulong charId) => MemberIds.Contains(charId);

    public void AddMember(ulong charId) {
        if (!MemberIds.Contains(charId)) {
            MemberIds.Add(charId);
        }
    }

    public void RemoveMember(ulong charId) {
        MemberIds.Remove(charId);
        SigilSlots.Remove(charId);

        if (LeaderCharId == charId && MemberIds.Count > 0) {
            LeaderCharId = MemberIds[0];
        }
    }

    public uint GetSigilSlot(ulong charId) => SigilSlots.GetValueOrDefault(charId);

    public bool HasGroupQuest => QuestId != 0 && QuestData is { Length: > 0 };

    public void ClearGroupQuest() {
        QuestId = 0;
        GoalId = 0;
        QuestData = null;
    }

}
