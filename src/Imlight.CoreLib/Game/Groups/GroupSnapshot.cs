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
 * An immutable view of one group, published by the GroupDirectory so zone,
 * combat and quest code can read group membership without asking an actor.
 * 
 * USAGE EXAMPLE:
 * if (GroupRegistry.TryGetGroup(charId, out var group)) { ... group.Members ... }
 * 
 * NOTE:
 * A snapshot is replaced wholesale on every change; never mutate one.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Imlight.CoreLib.Game.Groups;

internal sealed record GroupSnapshot(
    ulong GroupId,
    ulong ChannelId,
    ulong LeaderCharId,
    IReadOnlyList<GroupMemberSnapshot> Members,
    ulong QuestId,
    ulong GoalId) {

    public bool HasMember(ulong charId) => Members.Any(member => member.CharId == charId);

    public GroupMemberSnapshot GetMember(ulong charId) => Members.FirstOrDefault(member => member.CharId == charId);

}

internal sealed record GroupMemberSnapshot(
    ulong CharId,
    string ZonePath,
    IActorRef ZoneActor,
    IActorRef Session,
    uint SigilSlot) {

    public bool IsOnline => Session is not null;

}
