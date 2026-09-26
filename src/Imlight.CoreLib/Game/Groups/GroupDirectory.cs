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
 * The single authority for player groups: invites, membership, leadership,
 * group chat relay, the leader's shared quest and the group state each
 * client is shown. Plays the role the original chat server played for
 * groups.
 * 
 * USAGE EXAMPLE:
 * Created once by the GameServerPool. Sessions talk to it through
 * GroupService with GROUP_109_PROTOCOL messages; it answers each client with
 * generated GAME_5/WIZARD3_56 wire messages sent straight to the session.
 * 
 * NOTE:
 * One instance serves every realm and zone, so a group survives zone and
 * realm transfers. Each transfer is a new session; members re-announce
 * themselves on arrival and get the full group state again, which the
 * client applies idempotently. Messages for a member between sessions are
 * queued and replayed on arrival. Other systems read GroupRegistry.
 * 
 * TODO:
 * - The FriendsOnly flag of MSG_PARTYREQUESTJOIN comes from a privacy
 *   option the server does not store yet; it is always 0.
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Groups;

/// <summary>
/// Owns every group on the server and speaks the group protocol to each member's client.
/// </summary>
internal sealed class GroupDirectory : ReceiveProtocolDispatcher, IWithTimers {

    public const int MaxGroupSize = 4;
    public const uint MaxSigilSlot = 4;

    // Error codes of MSG_PARTYREQUESTRESPONSE, as the client's message boxes read them.
    private const int InviteErrorGroupFull = 1;
    private const int InviteErrorAlreadyGrouped = 2;
    private const int InviteErrorAlreadyInvited = 4;
    private const int InviteErrorNotFound = 5;
    private const int InviteErrorDeclined = 6;
    private const int InviteErrorUnavailable = 7;

    private const int JoinFailedSilently = 0;
    private const int JoinFailedGroupFull = 1;

    // The client displays its own /party lines with 0x52; everyone else's arrive without the "sent by me" bit.
    private const uint GroupChatFlags = 0x50;

    private const int MaxQueuedMessagesPerMember = 64;

    private static readonly TimeSpan s_inviteLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan s_departureGrace = TimeSpan.FromSeconds(60);

    public static IActorRef Instance { get; private set; }

    public ITimerScheduler Timers { get; set; }

    private readonly Dictionary<ulong, IActorRef> _sessions = [];
    private readonly Dictionary<ulong, GroupMemberProfile> _profiles = [];
    private readonly Dictionary<ulong, Group> _groupsById = [];
    private readonly Dictionary<ulong, Group> _groupsByMember = [];
    private readonly Dictionary<ulong, PendingInvite> _invitesByInvitee = [];
    private readonly Dictionary<ulong, List<IMessage>> _queuedMessages = [];

    public GroupDirectory() {
        Instance = Self;
    }

    public static Props Props()
        => Akka.Actor.Props.Create(() => new GroupDirectory());

    protected override bool AroundReceive(Receive receive, object message) {
        // A restart would rebuild the directory empty and silently dissolve every group on the server.
        try {
            return base.AroundReceive(receive, message);
        } catch (Exception ex) {
            Logger.Error("GroupDirectory failed to handle {MessageType}: {Exception}",
                Logger.Args(message.GetType().Name, ex));

            return true;
        }
    }

    #region Presence

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_MEMBERPRESENT))]
    private void ReceiveMemberPresent(GROUP_109_PROTOCOL.MSG_MEMBERPRESENT message) {
        var profile = message.Profile;
        var charId = profile.CharId;

        _sessions[charId] = message.Session;
        _profiles[charId] = profile;
        Timers.Cancel(DepartureTimerKey(charId));

        ReplayQueuedMessages(charId, message.Session);

        if (_groupsByMember.TryGetValue(charId, out var group) && group.IsAnnounced) {
            SendGroupState(group, charId);
            PublishSnapshot(group);
        }
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_MEMBERDEPARTED))]
    private void ReceiveMemberDeparted(GROUP_109_PROTOCOL.MSG_MEMBERDEPARTED message) {
        var charId = message.CharId;

        // A zone transfer can attach the next session before the previous one finishes disposing.
        if (!_sessions.TryGetValue(charId, out var session) || !session.Equals(message.Session)) {
            return;
        }

        _sessions.Remove(charId);
        var group = _groupsByMember.GetValueOrDefault(charId);

        if (message.IsLogout) {
            if (group is not null) {
                RemoveFromGroup(group, charId);
            }
            Forget(charId);

            return;
        }

        if (group is null) {
            Forget(charId);

            return;
        }

        PublishSnapshot(group);
        Timers.StartSingleTimer(
            DepartureTimerKey(charId),
            new GROUP_109_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED { CharId = charId },
            s_departureGrace
        );
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED))]
    private void ReceiveDepartureGraceExpired(GROUP_109_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED message) {
        var charId = message.CharId;
        if (_sessions.ContainsKey(charId)) {
            return;
        }

        if (_groupsByMember.TryGetValue(charId, out var group)) {
            Logger.Debug("Character {CharId} did not return in time and leaves group {GroupId}.",
                Logger.Args(charId, group.GroupId));

            RemoveFromGroup(group, charId);
        }

        Forget(charId);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_LEVELCHANGED))]
    private void ReceiveLevelChanged(GROUP_109_PROTOCOL.MSG_LEVELCHANGED message) {
        var charId = message.CharId;
        if (!_profiles.TryGetValue(charId, out var profile) || profile.Level == message.NewLevel) {
            return;
        }

        _profiles[charId] = profile with { Level = message.NewLevel };

        if (!_groupsByMember.TryGetValue(charId, out var group) || !group.IsAnnounced) {
            return;
        }

        foreach (var memberId in group.MemberIds.Where(memberId => memberId != charId)) {
            SendToMember(memberId, new GAME_5_PROTOCOL.MSG_PARTYLEVELUPUPDATE {
                DestinationCharacterID = memberId,
                CharacterID = charId,
                NewLevel = (int) message.NewLevel
            });
        }
    }

    #endregion

    #region Invites

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_INVITEMEMBER))]
    private void ReceiveInviteMember(GROUP_109_PROTOCOL.MSG_INVITEMEMBER message) {
        var inviterId = message.InviterCharId;
        if (!_profiles.TryGetValue(inviterId, out var inviterProfile) || !_sessions.ContainsKey(inviterId)) {
            return;
        }

        var inviteeId = message.TargetCandidateIds?
            .FirstOrDefault(candidate => candidate != inviterId && _sessions.ContainsKey(candidate)) ?? 0;
        if (inviteeId == 0) {
            RespondToInviter(inviterId, 0, InviteErrorNotFound);

            return;
        }

        var group = _groupsByMember.GetValueOrDefault(inviterId);
        var inviteeGroup = _groupsByMember.GetValueOrDefault(inviteeId);

        var errorCode = 0;
        if (message.CharIdsIgnoringInviter?.Contains(inviteeId) == true) {
            errorCode = InviteErrorUnavailable;
        } else if (group is not null && group.Count + group.PendingInviteeIds.Count >= MaxGroupSize) {
            errorCode = InviteErrorGroupFull;
        } else if (inviteeGroup is not null && inviteeGroup.IsAnnounced) {
            errorCode = InviteErrorAlreadyGrouped;
        } else if (_invitesByInvitee.ContainsKey(inviteeId)) {
            errorCode = InviteErrorAlreadyInvited;
        }

        if (errorCode != 0) {
            RespondToInviter(inviterId, inviteeId, errorCode);

            return;
        }

        group ??= CreateGroup(inviterId);
        group.PendingInviteeIds.Add(inviteeId);
        _invitesByInvitee[inviteeId] = new PendingInvite(group.GroupId, inviterId);

        SendToMember(inviteeId, new GAME_5_PROTOCOL.MSG_PARTYREQUESTJOIN {
            DestinationCharacterID = inviteeId,
            CharacterID = inviterId,
            GlobalID = inviterProfile.GameObjectId,
            PlayerNameBlob = inviterProfile.NameBlob,
            PartyID = group.GroupId,
            FriendsOnly = 0
        });

        Timers.StartSingleTimer(
            InviteTimerKey(inviteeId),
            new GROUP_109_PROTOCOL.MSG_INVITEEXPIRED { TargetCharId = inviteeId, GroupId = group.GroupId },
            s_inviteLifetime
        );

        Logger.Debug("Character {InviterId} invited {InviteeId} to group {GroupId}.",
            Logger.Args(inviterId, inviteeId, group.GroupId));
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_INVITEREPLY))]
    private void ReceiveInviteReply(GROUP_109_PROTOCOL.MSG_INVITEREPLY message) {
        var inviteeId = message.CharId;
        if (!_invitesByInvitee.TryGetValue(inviteeId, out var invite) || invite.GroupId != message.GroupId) {
            if (message.Accept) {
                SendJoinFailed(inviteeId, JoinFailedSilently);
            }

            return;
        }

        ForgetInvite(inviteeId);
        var group = _groupsById.GetValueOrDefault(invite.GroupId);

        if (!message.Accept) {
            RespondToInviter(invite.InviterCharId, inviteeId, InviteErrorDeclined);
            if (group is not null) {
                DissolveIfAbandoned(group);
            }

            return;
        }

        if (group is null) {
            SendJoinFailed(inviteeId, JoinFailedSilently);

            return;
        }

        JoinGroup(group, inviteeId);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_INVITEEXPIRED))]
    private void ReceiveInviteExpired(GROUP_109_PROTOCOL.MSG_INVITEEXPIRED message) {
        var inviteeId = message.TargetCharId;
        if (!_invitesByInvitee.TryGetValue(inviteeId, out var invite) || invite.GroupId != message.GroupId) {
            return;
        }

        ForgetInvite(inviteeId);
        SendInviteTimeout(inviteeId);

        if (_groupsById.TryGetValue(invite.GroupId, out var group)) {
            DissolveIfAbandoned(group);
        }
    }

    #endregion

    #region Membership

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_LEAVEGROUP))]
    private void ReceiveLeaveGroup(GROUP_109_PROTOCOL.MSG_LEAVEGROUP message) {
        if (_groupsByMember.TryGetValue(message.CharId, out var group)) {
            RemoveFromGroup(group, message.CharId);
        }
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_REMOVEMEMBER))]
    private void ReceiveRemoveMember(GROUP_109_PROTOCOL.MSG_REMOVEMEMBER message) {
        var groupOwnerId = message.IsStaffRequest ? message.TargetCharId : message.RequesterCharId;
        if (!_groupsByMember.TryGetValue(groupOwnerId, out var group) || !group.IsAnnounced) {
            return;
        }

        if (message.GroupId != 0 && message.GroupId != group.GroupId) {
            return;
        }

        var mayRemove = message.IsStaffRequest
            || (group.LeaderCharId == message.RequesterCharId && message.TargetCharId != message.RequesterCharId);
        if (!mayRemove || !group.HasMember(message.TargetCharId)) {
            return;
        }

        Logger.Debug("Character {TargetId} was removed from group {GroupId} by {RequesterId}.",
            Logger.Args(message.TargetCharId, group.GroupId, message.RequesterCharId));

        RemoveFromGroup(group, message.TargetCharId);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_CHANGELEADER))]
    private void ReceiveChangeLeader(GROUP_109_PROTOCOL.MSG_CHANGELEADER message) {
        var groupOwnerId = message.IsStaffRequest ? message.NewLeaderCharId : message.RequesterCharId;
        if (!_groupsByMember.TryGetValue(groupOwnerId, out var group) || !group.IsAnnounced) {
            return;
        }

        var mayChange = message.IsStaffRequest || group.LeaderCharId == message.RequesterCharId;
        if (!mayChange || !group.HasMember(message.NewLeaderCharId) || group.LeaderCharId == message.NewLeaderCharId) {
            return;
        }

        group.LeaderCharId = message.NewLeaderCharId;
        BroadcastLeader(group);
        PublishSnapshot(group);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_DISBANDGROUP))]
    private void ReceiveDisbandGroup(GROUP_109_PROTOCOL.MSG_DISBANDGROUP message) {
        if (!_groupsByMember.TryGetValue(message.RequesterCharId, out var group)) {
            return;
        }

        if (!message.IsStaffRequest && group.LeaderCharId != message.RequesterCharId) {
            return;
        }

        DisbandGroup(group);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_SETSIGILSLOT))]
    private void ReceiveSetSigilSlot(GROUP_109_PROTOCOL.MSG_SETSIGILSLOT message) {
        if (!_groupsByMember.TryGetValue(message.CharId, out var group) || !group.IsAnnounced) {
            return;
        }

        if (message.SigilSlot > MaxSigilSlot) {
            return;
        }

        group.SigilSlots[message.CharId] = message.SigilSlot;

        foreach (var memberId in group.MemberIds) {
            SendToMember(memberId, BuildSigilSlot(message.CharId, message.SigilSlot));
        }

        PublishSnapshot(group);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_SETGROUPQUEST))]
    private void ReceiveSetGroupQuest(GROUP_109_PROTOCOL.MSG_SETGROUPQUEST message) {
        if (!_groupsByMember.TryGetValue(message.RequesterCharId, out var group) || !group.IsAnnounced) {
            return;
        }

        // Every member's quest helper reports its tracked quest, but a group only follows its leader.
        if (group.LeaderCharId != message.RequesterCharId) {
            return;
        }

        group.QuestId = message.QuestId;
        group.GoalId = message.GoalId;
        group.QuestData = message.QuestData;

        foreach (var memberId in group.MemberIds.Where(memberId => memberId != group.LeaderCharId)) {
            SendGroupQuest(group, memberId);
        }

        PublishSnapshot(group);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_SETGROUPFOLLOW))]
    private void ReceiveSetGroupFollow(GROUP_109_PROTOCOL.MSG_SETGROUPFOLLOW message) {
        if (!_groupsByMember.TryGetValue(message.CharId, out var group) || !group.IsAnnounced) {
            return;
        }

        if (message.Follow && message.CharId != group.LeaderCharId) {
            SendGroupQuest(group, message.CharId);
        }
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_SUMMONGROUP))]
    private void ReceiveSummonGroup(GROUP_109_PROTOCOL.MSG_SUMMONGROUP message) {
        if (!_groupsByMember.TryGetValue(message.RequesterCharId, out var group)
            || !group.IsAnnounced
            || group.LeaderCharId != message.RequesterCharId) {
            return;
        }

        foreach (var memberId in group.MemberIds.Where(memberId => memberId != group.LeaderCharId)) {
            SendToMember(memberId, new WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPTELEPORT {
                PlayerGID = memberId,
                RequestingPlayerGID = group.LeaderCharId
            });
        }
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_GROUPCHAT))]
    private void ReceiveGroupChat(GROUP_109_PROTOCOL.MSG_GROUPCHAT message) {
        if (!_groupsByMember.TryGetValue(message.SenderCharId, out var group)
            || !group.IsAnnounced
            || group.ChannelId != message.ChannelId) {
            return;
        }

        foreach (var memberId in group.MemberIds) {
            if (memberId == message.SenderCharId || message.CharIdsIgnoringSender?.Contains(memberId) == true) {
                continue;
            }

            SendToMember(memberId, new GAME_5_PROTOCOL.MSG_CHANNELCHAT {
                SourceName = message.SourceName,
                SourceID = message.SenderCharId,
                Message = message.Message,
                TargetID = group.ChannelId,
                Filter = 0,
                Flags = GroupChatFlags
            });
        }
    }

    #endregion

    #region Group lifecycle

    private Group CreateGroup(ulong founderId) {
        var groupId = NewUniqueId();
        var channelId = NewUniqueId();
        var group = new Group(groupId, channelId, founderId);

        _groupsById[groupId] = group;
        _groupsByMember[founderId] = group;

        return group;
    }

    private void JoinGroup(Group group, ulong joinerId) {
        // Joining someone else's group abandons a group the joiner founded but nobody has joined yet.
        if (_groupsByMember.TryGetValue(joinerId, out var currentGroup)) {
            if (currentGroup.IsAnnounced) {
                DissolveIfAbandoned(group);

                return;
            }

            DeleteGroup(currentGroup);
        }

        if (group.IsFull) {
            SendJoinFailed(joinerId, JoinFailedGroupFull);
            DissolveIfAbandoned(group);

            return;
        }

        group.AddMember(joinerId);
        _groupsByMember[joinerId] = group;

        if (!group.IsAnnounced) {
            group.IsAnnounced = true;
            SendGroupState(group, group.LeaderCharId);
        } else {
            foreach (var memberId in group.MemberIds.Where(memberId => memberId != joinerId)) {
                SendToMember(memberId, BuildMemberUpdate(group, joinerId, memberId));
            }
        }

        SendGroupState(group, joinerId);
        PublishSnapshot(group);

        Logger.Debug("Character {JoinerId} joined group {GroupId} ({Count}/{Max}).",
            Logger.Args(joinerId, group.GroupId, group.Count, MaxGroupSize));
    }

    private void RemoveFromGroup(Group group, ulong charId) {
        if (!group.IsAnnounced) {
            // Only the founder is in an unannounced group; leaving it withdraws the outstanding invites.
            DeleteGroup(group);

            return;
        }

        var wasLeader = group.LeaderCharId == charId;
        group.RemoveMember(charId);
        _groupsByMember.Remove(charId);

        SendToMember(charId, new GAME_5_PROTOCOL.MSG_PARTYDISBAND { DestinationCharacterID = charId });

        if (group.Count <= 1) {
            DisbandGroup(group);
            GroupRegistry.Withdraw(group.ChannelId, [charId]);

            return;
        }

        foreach (var memberId in group.MemberIds) {
            SendToMember(memberId, new GAME_5_PROTOCOL.MSG_PARTYLEAVENOTIFICATION {
                DestinationCharacterID = memberId,
                CharacterID = charId,
                PartyTotalSize = (uint) group.Count,
                FromAdventureParty = 0
            });
        }

        if (wasLeader) {
            BroadcastLeader(group);
        }

        PublishSnapshot(group, [charId]);
    }

    private void DisbandGroup(Group group) {
        var formerMemberIds = group.MemberIds.ToList();

        if (group.IsAnnounced) {
            foreach (var memberId in formerMemberIds) {
                SendToMember(memberId, new GAME_5_PROTOCOL.MSG_PARTYDISBAND { DestinationCharacterID = memberId });
            }
        }

        DeleteGroup(group);

        Logger.Debug("Group {GroupId} disbanded.", Logger.Args(group.GroupId));
    }

    private void DeleteGroup(Group group) {
        foreach (var inviteeId in group.PendingInviteeIds.ToList()) {
            ForgetInvite(inviteeId);
            SendInviteTimeout(inviteeId);
        }

        foreach (var memberId in group.MemberIds) {
            if (_groupsByMember.TryGetValue(memberId, out var memberGroup) && memberGroup == group) {
                _groupsByMember.Remove(memberId);
            }
        }

        _groupsById.Remove(group.GroupId);
        GroupRegistry.Withdraw(group.ChannelId, group.MemberIds);
    }

    private void DissolveIfAbandoned(Group group) {
        if (!group.IsAnnounced && group.PendingInviteeIds.Count == 0) {
            DeleteGroup(group);
        }
    }

    private void ForgetInvite(ulong inviteeId) {
        if (!_invitesByInvitee.Remove(inviteeId, out var invite)) {
            return;
        }

        Timers.Cancel(InviteTimerKey(inviteeId));
        if (_groupsById.TryGetValue(invite.GroupId, out var group)) {
            group.PendingInviteeIds.Remove(inviteeId);
        }
    }

    private void Forget(ulong charId) {
        _profiles.Remove(charId);
        _queuedMessages.Remove(charId);
        Timers.Cancel(DepartureTimerKey(charId));
    }

    private ulong NewUniqueId() {
        ulong id;
        do {
            id = RandomGen.GenerateGUID();
        } while (id == 0 || _groupsById.ContainsKey(id) || _groupsById.Values.Any(group => group.ChannelId == id));

        return id;
    }

    #endregion

    #region Client state

    private void SendGroupState(Group group, ulong destinationId) {
        SendToMember(destinationId, new GAME_5_PROTOCOL.MSG_PARTYJOINNOTIFICATION {
            DestinationCharacterID = destinationId,
            ChannelID = group.ChannelId,
            PartyID = group.GroupId,
            PartyTotalSize = (uint) group.Count,
            FromAdventureParty = 0
        });

        foreach (var memberId in group.MemberIds.Where(memberId => memberId != destinationId)) {
            SendToMember(destinationId, BuildMemberUpdate(group, memberId, destinationId));
        }

        SendToMember(destinationId, BuildLeaderChange(group));

        foreach (var (memberId, sigilSlot) in group.SigilSlots) {
            SendToMember(destinationId, BuildSigilSlot(memberId, sigilSlot));
        }

        if (destinationId != group.LeaderCharId) {
            SendGroupQuest(group, destinationId);
        }
    }

    private void SendGroupQuest(Group group, ulong destinationId) {
        if (!group.HasGroupQuest) {
            return;
        }

        SendToMember(destinationId, new WIZARD3_56_PROTOCOL.MSG_SETGROUPQUEST {
            PlayerGID = group.LeaderCharId,
            QuestGID = group.QuestId,
            GoalGID = group.GoalId,
            Data = group.QuestData
        });
    }

    private GAME_5_PROTOCOL.MSG_PARTYUPDATE BuildMemberUpdate(Group group, ulong memberId, ulong destinationId) {
        var profile = _profiles.GetValueOrDefault(memberId);

        return new GAME_5_PROTOCOL.MSG_PARTYUPDATE {
            DestinationCharacterID = destinationId,
            PlayerNameBlob = profile?.NameBlob ?? [],
            CharacterID = memberId,
            GlobalID = profile?.GameObjectId ?? Wizard.GetGameObjectId(memberId),
            SchoolID = profile?.SchoolId ?? 0,
            Level = profile?.Level ?? 0,
            ZoneDisplayName = profile?.ZoneDisplayName ?? string.Empty,
            HasFilteredChat = 0,
            PartyTotalSize = (uint) group.Count,
            FromAdventureParty = 0,
            SigilSlot = group.GetSigilSlot(memberId),
            LeaderGID = group.LeaderCharId,
            QuestGID = group.QuestId,
            GoalGID = group.GoalId
        };
    }

    private static WIZARD3_56_PROTOCOL.MSG_CHANGEGROUPLEADER BuildLeaderChange(Group group)
        => new() {
            PlayerGID = group.LeaderCharId,
            RequestingPlayerGID = group.LeaderCharId
        };

    private static WIZARD3_56_PROTOCOL.MSG_SETSIGILSLOT BuildSigilSlot(ulong memberId, uint sigilSlot)
        => new() {
            PlayerGID = memberId,
            TargetGID = memberId,
            SigilSlot = sigilSlot
        };

    private void BroadcastLeader(Group group) {
        // The old leader's quest no longer leads; followers re-request the new leader's once the client
        // sees the follow nudge, and get it when the new leader's quest helper reports in.
        group.ClearGroupQuest();

        foreach (var memberId in group.MemberIds) {
            SendToMember(memberId, BuildLeaderChange(group));
            SendToMember(memberId, new WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPFOLLOW {
                Follow = 1,
                PlayerGID = group.LeaderCharId
            });
        }
    }

    private void RespondToInviter(ulong inviterId, ulong inviteeId, int errorCode) {
        // The client drops the whole response if the name blob does not unpack, so fall back to a real name.
        var inviteeProfile = _profiles.GetValueOrDefault(inviteeId);
        var nameBlob = inviteeProfile?.NameBlob ?? _profiles.GetValueOrDefault(inviterId)?.NameBlob ?? [];

        SendToMember(inviterId, new GAME_5_PROTOCOL.MSG_PARTYREQUESTRESPONSE {
            DestinationCharacterID = inviterId,
            TargetCharacterID = inviteeId,
            TargetGlobalID = inviteeProfile?.GameObjectId ?? 0,
            ErrorCode = errorCode,
            PlayerNameBlob = nameBlob
        });
    }

    private void SendJoinFailed(ulong charId, int errorCode) {
        // The client empties its group window on this message.
        if (IsInAnnouncedGroup(charId)) {
            return;
        }

        SendToMember(charId, new GAME_5_PROTOCOL.MSG_PARTYJOINFAILED {
            DestinationCharacterID = charId,
            ErrorCode = errorCode
        });
    }

    private void SendInviteTimeout(ulong charId) {
        // The client empties its group window on this message too.
        if (IsInAnnouncedGroup(charId)) {
            return;
        }

        SendToMember(charId, new GAME_5_PROTOCOL.MSG_PARTYREQUESTTIMEOUT { DestinationCharacterID = charId });
    }

    private bool IsInAnnouncedGroup(ulong charId)
        => _groupsByMember.TryGetValue(charId, out var group) && group.IsAnnounced;

    private void SendToMember(ulong charId, IMessage message) {
        if (_sessions.TryGetValue(charId, out var session)) {
            session.Tell(message);

            return;
        }

        // Only characters between sessions are queued; anyone else is simply offline.
        if (!_profiles.ContainsKey(charId)) {
            return;
        }

        if (!_queuedMessages.TryGetValue(charId, out var queue)) {
            queue = [];
            _queuedMessages[charId] = queue;
        }

        if (queue.Count < MaxQueuedMessagesPerMember) {
            queue.Add(message);
        }
    }

    private void ReplayQueuedMessages(ulong charId, IActorRef session) {
        if (!_queuedMessages.Remove(charId, out var queue)) {
            return;
        }

        foreach (var message in queue) {
            session.Tell(message);
        }
    }

    private void PublishSnapshot(Group group, IEnumerable<ulong> formerMemberIds = null) {
        formerMemberIds ??= [];

        if (!group.IsAnnounced) {
            GroupRegistry.Withdraw(group.ChannelId, formerMemberIds);

            return;
        }

        var members = group.MemberIds
            .Select(memberId => {
                var profile = _profiles.GetValueOrDefault(memberId);

                return new GroupMemberSnapshot(
                    CharId: memberId,
                    ZonePath: profile?.ZonePath ?? string.Empty,
                    ZoneActor: profile?.ZoneActor,
                    Session: _sessions.GetValueOrDefault(memberId),
                    SigilSlot: group.GetSigilSlot(memberId)
                );
            })
            .ToList();

        var snapshot = new GroupSnapshot(group.GroupId, group.ChannelId, group.LeaderCharId, members,
            group.QuestId, group.GoalId);
        GroupRegistry.Publish(snapshot, formerMemberIds);
    }

    private static string InviteTimerKey(ulong inviteeId) => $"invite-{inviteeId}";

    private static string DepartureTimerKey(ulong charId) => $"departure-{charId}";

    #endregion

    private sealed record PendingInvite(ulong GroupId, ulong InviterCharId);

}
