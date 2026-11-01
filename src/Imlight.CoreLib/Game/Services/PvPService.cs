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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * Announces the session to the PvPDirector and takes it back out. The PvP requests of the client are added
 * here as the matchmaking features arrive.
 * 
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory.
 * 
 * NOTE:
 * The client sends MSG_BUDDYREQUESTLIST once it is in the world after every login and zone transfer; that is
 * when this session announces itself to the director.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class PvPService(SessionActor sessionActor) : MessageService(sessionActor) {

    private ulong _charId;
    private bool _isPresent;
    private bool _isLoggingOut;
    private string _realmName;
    private PvPArenaReturn _arenaReturn;
    private readonly PvPWindowState _window = new();

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PvPService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST))]
    private void ReceiveBuddyRequestList(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var realmName = GetRealmName();
        if (realmName is null) {
            Logger.Error("PvP presence of {CharId} skipped: the realm name is unknown.", Logger.Args(wizard.CharId));

            return;
        }

        _charId = wizard.CharId;
        _isPresent = true;

        TellDirector(new PVP_111_PROTOCOL.MSG_PLAYERPRESENT {
            Session = SessionActor.ActorRef,
            Profile = PvPPlayerProfile.FromWizard(wizard, SessionActor.GetZoneActor(), realmName),
            PlayerObject = GetActiveGameObject()
        });
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_KIOSKUSED))]
    private void ReceiveKioskUsed(PVP_111_PROTOCOL.MSG_KIOSKUSED message)
        => _window.KioskUsed(message.LeagueId);

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_PVP5THAGEOPENPVPWINDOW))]
    private void ReceiveOpenPvPWindow(WIZARD3_56_PROTOCOL.MSG_PVP5THAGEOPENPVPWINDOW message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        SendAll(_window.OpenWindow(message, SessionActor.ActorRef, GetActiveGameObject(), wizard, DateTime.UtcNow));
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_PVP5THAGECANJOINMATCH))]
    private void ReceiveCanJoinMatch(WIZARD3_56_PROTOCOL.MSG_PVP5THAGECANJOINMATCH message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        SendAll(_window.CanJoin(message, SessionActor.ActorRef, GetActiveGameObject(), wizard, DateTime.UtcNow));
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PREPVPKIOSK))]
    private void ReceivePrePvPKiosk(WIZARD_12_PROTOCOL.MSG_PREPVPKIOSK message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var replies = _window.PrePvPKioskEcho(message, wizard);

        SendAll(replies);
        AnnounceWindow(replies.Count, wizard);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PVPUPDATEREQUEST))]
    private void ReceivePvPUpdateRequest(WIZARD_12_PROTOCOL.MSG_PVPUPDATEREQUEST message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var replies = _window.UpdateRequest(message, wizard);

        SendAll(replies);
        AnnounceWindow(replies.Count, wizard);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PVPINTENT))]
    private void ReceivePvPIntent(WIZARD_12_PROTOCOL.MSG_PVPINTENT message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var (replies, toDirector) = _window.Intent(message, SessionActor.ActorRef, GetActiveGameObject(), wizard, DateTime.UtcNow);

        SendAll(replies);
        if (toDirector is not null) {
            TellDirector(toDirector);
        }
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_EXPANDPVPSEARCH))]
    private void ReceiveExpandPvPSearch(WIZARD_12_PROTOCOL.MSG_EXPANDPVPSEARCH message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var expand = _window.ExpandSearch(message, wizard);
        if (expand is not null) {
            TellDirector(expand);
        }
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PVPCONFIRM))]
    private void ReceivePvPConfirm(WIZARD_12_PROTOCOL.MSG_PVPCONFIRM message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        // A player in a PvE duel can answer once the duel is over; the match waits for them.
        if (message.Confirm != 0 && (wizard.IsInDuel || wizard.IsInCombatGrace)) {
            SendToSocket(PvPArenaErrors.Message(PvPArenaErrors.PLAYER_INELIGIBLE));

            return;
        }

        TellDirector(new PVP_111_PROTOCOL.MSG_CONFIRMMATCH {
            CharId = wizard.CharId,
            TournamentNameId = message.TournamentNameID,
            Accept = message.Confirm != 0
        });
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_ENTERMATCH))]
    private void ReceiveEnterMatch(PVP_111_PROTOCOL.MSG_ENTERMATCH message)
        => Teleport(message.ArenaZone, doTeleportEffects: false, makePrivate: true, ownerCharId: message.InstanceOwnerId);

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_MATCHJOINED))]
    private void ReceiveMatchJoined(PVP_111_PROTOCOL.MSG_MATCHJOINED message)
        => _arenaReturn = new PvPArenaReturn(message.ReturnZone, message.ReturnZoneDisplayName, message.ReturnLocation);

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_LEAVEARENA))]
    private void ReceiveLeaveArena(PVP_111_PROTOCOL.MSG_LEAVEARENA message) {
        _arenaReturn = null;

        Teleport(message.ReturnZone, doTeleportEffects: true, makePrivate: false, destinationLocation: message.ReturnLocation);
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

        if (isLogout) {
            RestoreLocationBeforeArena();
        }

        TellDirector(new PVP_111_PROTOCOL.MSG_PLAYERDEPARTED {
            CharId = _charId,
            Session = SessionActor.ActorRef,
            IsLogout = isLogout
        });
    }

    private void RestoreLocationBeforeArena() {
        // The attach into the arena saved the arena as the zone of the wizard, and nobody else can join that
        // private instance; the next login must start where the player queued.
        var wizard = GetActiveWizard();
        if (_arenaReturn is null || wizard is null) {
            return;
        }

        wizard.SetZone(_arenaReturn.Zone, _arenaReturn.ZoneDisplayName);

        var location = Util.GetVectorFromCompactString(_arenaReturn.Location);
        if (location.X != 0 || location.Y != 0 || location.Z != 0) {
            wizard.SetPersistentLocation(new Imcodec.Math.Vector3(location.X, location.Y, location.Z));
            wizard.SetPersistentOrientation(location.W);
        }

        _arenaReturn = null;
    }

    private void AnnounceWindow(int replyCount, Wizard wizard) {
        if (replyCount > 0) {
            TellDirector(new PVP_111_PROTOCOL.MSG_WINDOWOPENED { CharId = wizard.CharId, TournamentNameId = _window.TournamentNameId, LeagueId = _window.LeagueId, SeasonId = _window.SeasonId });
        }
    }

    private void SendAll(IReadOnlyList<IMessage> messages) {
        foreach (var message in messages) {
            SendToSocket(message);
        }
    }

    private string GetRealmName() {
        if (_realmName is not null) {
            return _realmName;
        }

        try {
            _realmName = AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(new SERVER_100_PROTOCOL.MSG_QUERYSERVER())?.RealmName;
        } catch (Exception ex) {
            Logger.Error("PvP could not ask the server for its realm name. {Exception}", Logger.Args(ex));
        }

        return _realmName;
    }

    private static void TellDirector(IServerMessage message)
        => PvPDirector.Instance?.Tell(message);

}
