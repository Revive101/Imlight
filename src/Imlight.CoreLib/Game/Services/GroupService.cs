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
 * Receives the client's group requests (invite, accept, decline, leave,
 * leader change, removal, sigil slot preference, the leader's quest,
 * follow and summon) and forwards them to the GroupDirectory with this
 * session's identity. Also answers the prompt to join a group mate's fight
 * and the request to teleport to a group mate.
 * 
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory.
 * 
 * NOTE:
 * The client leaves the source character fields of its group requests
 * empty, so the session's own wizard is always the requester. The client
 * sends MSG_BUDDYREQUESTLIST once it is in the world after every login and
 * zone transfer; that is when this session announces itself to the
 * directory and receives the group state.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class GroupService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const float QueryGroupMateTimeoutInSeconds = 2;

    private ulong _charId;
    private bool _isPresent;
    private bool _isLoggingOut;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new GroupService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST))]
    private void ReceiveBuddyRequestList(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        _charId = wizard.CharId;
        _isPresent = true;

        TellDirectory(new GROUP_109_PROTOCOL.MSG_MEMBERPRESENT {
            Session = SessionActor.ActorRef,
            Profile = GroupMemberProfile.FromWizard(wizard, SessionActor.GetZoneActor())
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_PARTYREQUESTINVITE))]
    private void ReceivePartyRequestInvite(GAME_5_PROTOCOL.MSG_PARTYREQUESTINVITE message) {
        var wizard = GetActiveWizard();

        TellDirectory(new GROUP_109_PROTOCOL.MSG_INVITEMEMBER {
            InviterCharId = wizard.CharId,
            TargetCandidateIds = GetInviteeCandidates(message),
            CharIdsIgnoringInviter = BuddyRelationshipCollection.GetCharactersWhoBlocked(wizard.CharId)
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_PARTYREQUESTACCEPT))]
    private void ReceivePartyRequestAccept(GAME_5_PROTOCOL.MSG_PARTYREQUESTACCEPT message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_INVITEREPLY {
            CharId = GetActiveWizard().CharId,
            GroupId = message.PartyID,
            Accept = true
        });

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_PARTYREQUESTDECLINE))]
    private void ReceivePartyRequestDecline(GAME_5_PROTOCOL.MSG_PARTYREQUESTDECLINE message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_INVITEREPLY {
            CharId = GetActiveWizard().CharId,
            GroupId = message.PartyID,
            Accept = false
        });

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_PARTYLEAVE))]
    private void ReceivePartyLeave(GAME_5_PROTOCOL.MSG_PARTYLEAVE message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_LEAVEGROUP {
            CharId = GetActiveWizard().CharId
        });

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_CHANGEGROUPLEADER))]
    private void ReceiveChangeGroupLeader(WIZARD3_56_PROTOCOL.MSG_CHANGEGROUPLEADER message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_CHANGELEADER {
            RequesterCharId = GetActiveWizard().CharId,
            NewLeaderCharId = message.PlayerGID
        });

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REMOVEPARTYMEMBER))]
    private void ReceiveRemovePartyMember(WIZARD3_56_PROTOCOL.MSG_REMOVEPARTYMEMBER message) {
        // The live client sends this from the adventure party roster; it only
        // touches a regular group when it names the requester's group.
        if (message.PartyGID == 0) {
            return;
        }

        TellDirectory(new GROUP_109_PROTOCOL.MSG_REMOVEMEMBER {
            RequesterCharId = GetActiveWizard().CharId,
            TargetCharId = message.PlayerGID,
            GroupId = message.PartyGID
        });
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTSIGILSLOT))]
    private void ReceiveRequestSigilSlot(WIZARD3_56_PROTOCOL.MSG_REQUESTSIGILSLOT message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_SETSIGILSLOT {
            CharId = GetActiveWizard().CharId,
            SigilSlot = message.SigilSlot
        });

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_LEVELCHANGED))]
    private void ReceiveLevelChanged(GROUP_109_PROTOCOL.MSG_LEVELCHANGED message)
        => TellDirectory(message);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_SETGROUPQUEST))]
    private void ReceiveSetGroupQuest(WIZARD3_56_PROTOCOL.MSG_SETGROUPQUEST message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_SETGROUPQUEST {
            RequesterCharId = GetActiveWizard().CharId,
            QuestId = message.QuestGID,
            GoalId = message.GoalGID,
            QuestData = message.Data
        });

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPFOLLOW))]
    private void ReceiveRequestGroupFollow(WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPFOLLOW message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_SETGROUPFOLLOW {
            CharId = GetActiveWizard().CharId,
            Follow = message.Follow != 0
        });

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPTELEPORT))]
    private void ReceiveRequestGroupTeleport(WIZARD3_56_PROTOCOL.MSG_REQUESTGROUPTELEPORT message)
        => TellDirectory(new GROUP_109_PROTOCOL.MSG_SUMMONGROUP {
            RequesterCharId = GetActiveWizard().CharId
        });

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GOTOFRIENDLYPLAYER))]
    private void ReceiveGotoFriendlyPlayer(WIZARD_12_PROTOCOL.MSG_GOTOFRIENDLYPLAYER message) {
        // The group window's "go to leader" and the summon prompt both land here.
        var wizard = GetActiveWizard();
        var targetCharId = message.TargetCharacterID;

        if (!GroupRegistry.AreGrouped(wizard.CharId, targetCharId)
            || !TryGetOnlinePlayer(targetCharId, out var target)
            || target.CurrentRealm != GetRealmName()) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_GOTOPLAYERRESP { TargetCharacterID = targetCharId, Error = 1 });

            return;
        }

        var targetCharacter = Context.ActorSelection(target.ActorPath)
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(
                message: new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                timeout: TimeSpan.FromSeconds(QueryGroupMateTimeoutInSeconds))
            .Result;
        if (targetCharacter?.Wizard is null) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_GOTOPLAYERRESP { TargetCharacterID = targetCharId, Error = 1 });

            return;
        }

        Teleport(
            destinationZone: target.CurrentZone,
            destinationLocation: Util.GetCompactStringFromVector((Vector4) targetCharacter.Wizard.Location),
            doTeleportEffects: true,
            ownerCharId: targetCharId
        );
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_GotoPlayerConfirm))]
    private void ReceiveGotoPlayerConfirm(WIZARD2_53_PROTOCOL.MSG_GotoPlayerConfirm message) {
        // The client only answers the prompt when the player says yes. A sigil sends that prompt when it
        // holds a circle for a group mate; only group mates are answered here.
        var wizard = GetActiveWizard();
        if (wizard.IsInDuel || !GroupRegistry.AreGrouped(wizard.CharId, message.TargetCharacterID)) {
            return;
        }

        SessionActor.GetZoneActor()?.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Sender = SessionActor.ActorRef,
            Messages = [
                new GROUP_109_PROTOCOL.MSG_JOINRESERVEDDUEL {
                    CharId = wizard.CharId,
                    PlayerActor = SessionActor.ActorRef,
                    PlayerObject = GetActiveGameObject()
                }
            ],
            Targets = ZoneBroadcastTarget.Sigils
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        => AnnounceDeparture(isLogout: true);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        => AnnounceDeparture(isLogout: true);

    protected override void OnPreDispose() {
        AnnounceDeparture(_isLoggingOut);

        base.OnPreDispose();
    }

    private void AnnounceDeparture(bool isLogout) {
        if (!_isPresent) {
            return;
        }

        _isPresent = false;
        _isLoggingOut |= isLogout;

        TellDirectory(new GROUP_109_PROTOCOL.MSG_MEMBERDEPARTED {
            CharId = _charId,
            Session = SessionActor.ActorRef,
            IsLogout = isLogout
        });
    }

    private static List<ulong> GetInviteeCandidates(GAME_5_PROTOCOL.MSG_PARTYREQUESTINVITE message) {
        // Inviting from the world sends the target's character and object IDs; inviting from the
        // friends list sends the same ID in both fields.
        var candidates = new List<ulong> { message.TargetCharacterID };

        if (Wizard.TryGetCharacterId(message.TargetGlobalID, out var fromObjectId)) {
            candidates.Add(fromObjectId);
        }

        if (Wizard.TryGetCharacterId(message.TargetCharacterID, out var fromCharacterField)) {
            candidates.Add(fromCharacterField);
        }

        return [.. candidates.Where(candidate => candidate != 0).Distinct()];
    }

    private string GetRealmName()
        => AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(new SERVER_100_PROTOCOL.MSG_QUERYSERVER())?.RealmName;

    private static void TellDirectory(IServerMessage message)
        => GroupDirectory.Instance?.Tell(message);

}
