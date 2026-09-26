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
 * COMBAT DUEL SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Holds player circles open for the group mates of whoever is fighting on a
 * sigil, so only those group mates can take them, and picks the circle each
 * player gets from their sigil slot preference.
 * 
 * USAGE EXAMPLE:
 * Owned by CombatDuelComponent. The component asks for a circle whenever a
 * player joins and reserves circles for that player's group right after.
 * 
 * NOTE:
 * Runs on the duel component's actor thread, a plain helper like
 * TutorialDuelDirector. Group membership comes from GroupRegistry, so it can
 * trail the directory by one change; a reservation that no longer fits is
 * dropped the next time it is checked. Only group mates in the same zone
 * instance and not already fighting are reserved for.
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
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Combat;

internal sealed class CombatGroupReservations {

    // Circles 0-3 belong to the creatures, 4-7 to the players, in the order the sigil template lists them.
    private const int FirstPlayerCircleIndex = 4;

    private readonly Dictionary<ulong, int> _slotIndexByCharId = [];

    public int Count => _slotIndexByCharId.Count;

    public bool IsReservedFor(ulong charId) => _slotIndexByCharId.ContainsKey(charId);

    public void Release(ulong charId) => _slotIndexByCharId.Remove(charId);

    public void Clear() => _slotIndexByCharId.Clear();

    /// <summary>
    /// The circle <paramref name="charId"/> should take: their reservation, else their preferred free circle,
    /// else the first free circle nobody else holds. Null when every free circle is held for someone else.
    /// </summary>
    public CombatDuelSubCircle ChoosePlayerCircle(ulong charId, IReadOnlyList<CombatDuelSubCircle> subCircles) {
        var playerCircles = GetPlayerCircles(subCircles);

        if (_slotIndexByCharId.TryGetValue(charId, out var reservedIndex)) {
            var reserved = playerCircles.FirstOrDefault(circle => circle.SlotIndex == reservedIndex);
            if (reserved is not null && !reserved.Occupied) {
                return reserved;
            }
        }

        var preferred = GetPreferredCircle(charId, playerCircles);
        if (preferred is not null && !preferred.Occupied && !IsHeldForSomeoneElse(preferred, charId)) {
            return preferred;
        }

        return playerCircles.FirstOrDefault(circle => !circle.Occupied && !IsHeldForSomeoneElse(circle, charId));
    }

    /// <summary>
    /// Reserves free circles for the group mates of <paramref name="fighterCharId"/> who are in
    /// <paramref name="zoneActor"/> and not yet fighting here.
    /// </summary>
    /// <returns>The group mates that received a new reservation.</returns>
    public List<GroupMemberSnapshot> ReserveForGroupOf(ulong fighterCharId, IActorRef zoneActor,
                                                      IReadOnlyList<CombatDuelSubCircle> subCircles) {
        var newlyReserved = new List<GroupMemberSnapshot>();
        if (!GroupRegistry.TryGetGroup(fighterCharId, out var group)) {
            return newlyReserved;
        }

        var playerCircles = GetPlayerCircles(subCircles);
        foreach (var member in group.Members) {
            if (member.CharId == fighterCharId
                || _slotIndexByCharId.ContainsKey(member.CharId)
                || !IsNearby(member, zoneActor)
                || IsFighting(member.CharId, subCircles)) {
                continue;
            }

            var preferred = member.SigilSlot is > 0 and <= GroupDirectory.MaxSigilSlot
                ? playerCircles.ElementAtOrDefault((int) member.SigilSlot - 1)
                : null;
            var circle = preferred is not null && IsFree(preferred)
                ? preferred
                : playerCircles.FirstOrDefault(IsFree);
            if (circle is null) {
                break;
            }

            _slotIndexByCharId[member.CharId] = circle.SlotIndex;
            newlyReserved.Add(member);
        }

        return newlyReserved;
    }

    /// <summary>
    /// Drops reservations whose group mate left the group, the zone or the game, or is no longer grouped
    /// with anyone fighting here.
    /// </summary>
    public void DropStale(IActorRef zoneActor, IReadOnlyList<CombatDuelSubCircle> subCircles) {
        var fighterCharIds = GetFighterCharIds(subCircles);

        foreach (var charId in _slotIndexByCharId.Keys.ToList()) {
            var stillWanted = GroupRegistry.TryGetGroup(charId, out var group)
                && fighterCharIds.Any(group.HasMember)
                && IsNearby(group.GetMember(charId), zoneActor);
            if (!stillWanted) {
                _slotIndexByCharId.Remove(charId);
            }
        }
    }

    /// <summary>
    /// True when every player circle is filled by members of one group, which earns them the first turn.
    /// </summary>
    public static bool IsFullGroup(IReadOnlyList<CombatDuelSubCircle> subCircles) {
        var fighterCharIds = GetFighterCharIds(subCircles);
        if (fighterCharIds.Count < GroupDirectory.MaxGroupSize) {
            return false;
        }

        return GroupRegistry.TryGetGroup(fighterCharIds[0], out var group) && fighterCharIds.All(group.HasMember);
    }

    public static bool TryGetCharId(CombatDuelSubCircle circle, out ulong charId) {
        charId = 0;

        return circle is { Occupied: true, OccupiedTeam: CombatTeam.Player, IsSummonedMinion: false }
            && Wizard.TryGetCharacterId(circle.ParticipantObject.m_globalID, out charId);
    }

    private bool IsFree(CombatDuelSubCircle circle)
        => !circle.Occupied && !_slotIndexByCharId.ContainsValue(circle.SlotIndex);

    private bool IsHeldForSomeoneElse(CombatDuelSubCircle circle, ulong charId)
        => _slotIndexByCharId.Any(reservation => reservation.Value == circle.SlotIndex && reservation.Key != charId);

    private static CombatDuelSubCircle GetPreferredCircle(ulong charId, List<CombatDuelSubCircle> playerCircles) {
        if (!GroupRegistry.TryGetGroup(charId, out var group)) {
            return null;
        }

        var sigilSlot = group.GetMember(charId)?.SigilSlot ?? 0;

        return sigilSlot is > 0 and <= GroupDirectory.MaxSigilSlot
            ? playerCircles.ElementAtOrDefault((int) sigilSlot - 1)
            : null;
    }

    private static List<CombatDuelSubCircle> GetPlayerCircles(IReadOnlyList<CombatDuelSubCircle> subCircles)
        => [.. subCircles
            .Skip(FirstPlayerCircleIndex)
            .Take(GroupDirectory.MaxGroupSize)
            .Where(circle => circle is not null)];

    private static List<ulong> GetFighterCharIds(IReadOnlyList<CombatDuelSubCircle> subCircles) {
        var charIds = new List<ulong>();
        foreach (var circle in subCircles) {
            if (TryGetCharId(circle, out var charId)) {
                charIds.Add(charId);
            }
        }

        return charIds;
    }

    private static bool IsFighting(ulong charId, IReadOnlyList<CombatDuelSubCircle> subCircles)
        => GetFighterCharIds(subCircles).Contains(charId);

    private static bool IsNearby(GroupMemberSnapshot member, IActorRef zoneActor)
        => member is { IsOnline: true, ZoneActor: not null } && member.ZoneActor.Equals(zoneActor);

}
