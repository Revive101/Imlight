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
 * PET GAMES SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Runs a player's pet game: the join and its energy, INIT, READY and START, the game's play, the end
 * screen with its stats, XP and wins, the snack after the game, and the skip.
 *
 * USAGE EXAMPLE:
 * The kiosk window sends MSG_PETGAMEJOIN {Game, Track}. The player is sent to the game's own zone, and once
 * there gets MSG_PETGAMEINIT; the client answers MSG_PETGAMEREADY, and the game plays from MSG_PETGAMESTART
 * to MSG_PETGAMEEND. Closing the game window sends the player back to where they joined.
 *
 * NOTE:
 * HatchingService also handles MSG_PETGAMEREADY, MSG_PETGAMEDATA and MSG_PETGAMEENDING, and the session
 * gives every service a copy. Each service takes only the messages of the window it opened: this one acts
 * on a message only while it has a session, and on DATA and ENDING only when they name that session's game.
 * Quitting a game gives no results and no refund. Leaving the zone or the session aborts the game, which
 * refunds part of its energy. The zone transfer starts a new session, so the join is carried over in
 * PetGameVisits. The client answers INIT only once the zone's game object is in its world, which
 * PetGameRoomComponent reports, so INIT waits for that and for the attach. Energy is charged on READY.
 *
 * TODO:
 * - Should a game time out when the client never sends READY or never finishes?
 *
 * Created by: Jay with Claude Code (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Globalization;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Games;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using PetGameConfig = Imlight.CoreLib.Game.Pet.Config.PetGameConfig;

namespace Imlight.CoreLib.Game.Services;

internal class PetGamesService(SessionActor sessionActor) : MessageService(sessionActor), IPetGameHost {

    private enum JoinRefusal {

        None,
        TrackNotANumber,
        GameInPlay,
        NoHatchedPet,
        UnknownGame,
        InOtherPetActivity,
        NotEnoughEnergy,
        NoGameZone

    }

    private const byte QUIT_DATA_COMMAND = 3;
    private const byte SNACK_PICK_DATA_COMMAND = 4;
    private const byte JOIN_SUCCEEDED = 1;
    private const byte JOIN_FAILED = 0;
    private const int SKIPPED_GAME_TRACK_INDEX = 0;
    private const PropertyFlags END_DATA_PROPERTY_MASK = PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Public;
    private const string NO_HATCHED_PET_TEXT = "You need to equip a hatched pet to play pet games.";
    private const string NOT_ENOUGH_ENERGY_TEXT = "You do not have enough energy to play this game.";
    private const string GAME_IN_PLAY_TEXT = "You are already playing a pet game.";
    private const string OTHER_PET_ACTIVITY_TEXT = "Your pet is busy with something else right now.";
    private const string GAME_UNAVAILABLE_TEXT = "This pet game is not available right now.";

    private PetGameSession _session;
    private PetGameVisit _visit;
    private bool _attachedToVisitZone;
    private bool _visitRoomJoined;

    PetGameSession IPetGameHost.Session => _session;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PetGamesService(parentActor));

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEJOIN))]
    private void ReceivePetGameJoin(PET_9_PROTOCOL.MSG_PETGAMEJOIN message) {
        var wizard = GetActiveWizard();
        var gameName = (string) message.Game;
        var trackText = (string) message.Track;
        Logger.Debug("{CharId} asks to join the pet game {Game} on track {Track}.", Logger.Args(wizard.CharId, gameName, trackText));

        var refusal = CheckJoin(wizard, gameName, trackText, out var trackIndex, out var zone);
        if (refusal != JoinRefusal.None) {
            RefuseJoin(wizard, gameName, trackText, refusal);

            return;
        }

        PetGameVisits.Begin(wizard.CharId, new PetGameVisit(gameName, trackIndex, zone, wizard.Zone,
            Util.GetCompactStringFromVector(wizard.Location, wizard.Orientation)));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEJOINRSP { Game = gameName, Success = JOIN_SUCCEEDED });
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ENTERMINIGAME());
        Logger.Information("{CharId} joins the pet game {Game} on track {Track} and goes to {Zone}.",
            Logger.Args(wizard.CharId, gameName, trackIndex, zone));

        Teleport(zone, doTeleportEffects: false, makePrivate: true);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PETGAMEROOMJOINED))]
    private void ReceivePetGameRoomJoined(ZONE_102_PROTOCOL.MSG_PETGAMEROOMJOINED message) {
        if (!PetGameVisits.TryGet(GetActiveWizard().CharId, out var visit) || !visit.IsGame(message.GameName)) {
            return;
        }

        _visit = visit;
        _visitRoomJoined = true;
        TryOpenVisitGame();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();
        if (_session is not null && !string.Equals(wizard.Zone, _session.Zone, StringComparison.OrdinalIgnoreCase)) {
            AbortSession(sendEnergy: true);
        }

        if (!PetGameVisits.TryGet(wizard.CharId, out var visit)) {
            return;
        }

        if (!visit.IsZone(wizard.Zone)) {
            PetGameVisits.End(wizard.CharId);

            return;
        }

        _visit = visit;
        _attachedToVisitZone = true;
        TryOpenVisitGame();
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEREADY))]
    private void ReceivePetGameReady(PET_9_PROTOCOL.MSG_PETGAMEREADY message) {
        if (_session is not { State: PetGameSessionState.AwaitingReady } session) {
            return;
        }

        if (!TryChargeEnergy(session)) {
            Logger.Warning("{CharId} could not start the pet game {Game}: not enough energy on READY.",
                Logger.Args(session.Wizard.CharId, session.GameName));
            InformGameClient(NOT_ENOUGH_ENERGY_TEXT);
            EndSession();
            ReturnFromVisit();

            return;
        }

        session.State = PetGameSessionState.Playing;
        Logger.Debug("{CharId} starts the pet game {Game} on track {Track}.",
            Logger.Args(session.Wizard.CharId, session.GameName, session.TrackIndex));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMESTART { Game = session.GameName, Data = session.Game.CreateStartData(session) });
        session.Game.Start(this);
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEDATA))]
    private void ReceivePetGameData(PET_9_PROTOCOL.MSG_PETGAMEDATA message) {
        byte[] data = message.Data;
        if (_session is null || !_session.IsGame(message.Game) || data is not { Length: > 0 }) {
            return;
        }

        using var payload = new BitReader(data);
        var command = payload.ReadUInt8();
        switch (command) {
            case QUIT_DATA_COMMAND:
                EndSession();
                ReturnFromVisit();
                break;
            case SNACK_PICK_DATA_COMMAND when _session.State == PetGameSessionState.Results && data.Length >= sizeof(byte) + sizeof(ulong):
                FeedSnack(payload.ReadUInt64());
                break;
            default:
                if (_session.State == PetGameSessionState.Playing) {
                    _session.Game.ReceiveData(this, command, payload);
                }

                break;
        }
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEDANCE))]
    private void ReceivePetGameDance(PET_9_PROTOCOL.MSG_PETGAMEDANCE message) {
        if (_session is not { State: PetGameSessionState.Playing } session) {
            return;
        }

        session.Game.ReceiveMessage(this, message);
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEENDING))]
    private void ReceivePetGameEnding(PET_9_PROTOCOL.MSG_PETGAMEENDING message)
        => CloseGame(message.Game);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIPFEED))]
    private void ReceivePetGameSkipFeed(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIPFEED message)
        => CloseGame(message.Game);

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIP))]
    private void ReceivePetGameSkip(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIP message) {
        // todo: the skip names no track, so it plays the first one. Which track does live use?
        if (!TryBeginSession(message.Game, SKIPPED_GAME_TRACK_INDEX, out var session)) {
            return;
        }

        if (!TryChargeEnergy(session)) {
            Logger.Warning("{CharId} could not skip the pet game {Game}: not enough energy.",
                Logger.Args(session.Wizard.CharId, session.GameName));
            InformGameClient(NOT_ENOUGH_ENERGY_TEXT);
            EndSession();

            return;
        }

        session.State = PetGameSessionState.Results;
        PetActivityGuard.End(session.Wizard);

        // todo: what does live give for a skipped game? This gives the game's median score and counts no win.
        session.Score = session.Game.SkipScore;
        session.Wins = GetWins(session);

        // The client builds its own end screen from Bonus, as both the score and the XP.
        PetXpAward.GiveStatMods(SessionActor.ActorRef, GetActiveGameObject(), session.Wizard, session.Pet, session.Track, PetXpSource.Game,
            session.GetScoreXp(session.Score),
            (_, xp) => new WIZARD2_53_PROTOCOL.MSG_PETGAMESKIP {
                GlobalID = session.Pet.m_globalID.Full,
                Game = session.GameName,
                Bonus = (byte) Math.Min(xp.Granted, byte.MaxValue),
            });
    }

    protected override void OnPreDispose() {
        AbortSession(sendEnergy: false);
        base.OnPreDispose();
    }

    void IPetGameHost.Send(IMessage message)
        => SendToSocket(message);

    void IPetGameHost.Finish(PetGameOutcome outcome) {
        if (_session is not { State: PetGameSessionState.Playing } session) {
            return;
        }

        session.State = PetGameSessionState.Results;
        session.Score = outcome.Score;
        PetActivityGuard.End(session.Wizard);
        session.Wins = outcome.Won
            ? WizardPetCollection.AddGameWin(session.Wizard.CharId, session.Pet.m_globalID.Full, session.GameName)
            : GetWins(session);
        Logger.Debug("{CharId} finished the pet game {Game} with score {Score} (won: {Won}).",
            Logger.Args(session.Wizard.CharId, session.GameName, outcome.Score, outcome.Won));

        PetXpAward.GiveStatMods(SessionActor.ActorRef, GetActiveGameObject(), session.Wizard, session.Pet, session.Track, PetXpSource.Game,
            session.GetScoreXp(outcome.Score),
            (stats, xp) => new PET_9_PROTOCOL.MSG_PETGAMEEND {
                Game = session.GameName,
                Data = SerializeEndData(session.Score, stats.Mods, xp.Granted, session.Wins),
            });
    }

    private JoinRefusal CheckJoin(Wizard wizard, string gameName, string trackText, out int trackIndex, out string zone) {
        zone = null;
        if (!int.TryParse(trackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out trackIndex)) {
            return JoinRefusal.TrackNotANumber;
        }

        var refusal = CheckSessionStart(wizard, gameName, trackIndex, out _, out _);
        if (refusal != JoinRefusal.None) {
            return refusal;
        }

        if (PetActivityGuard.TryGetActivity(wizard, out var activity) && activity != PetActivity.Game) {
            return JoinRefusal.InOtherPetActivity;
        }

        return PetGameZones.TryGetZone(gameName, trackIndex, out zone) ? JoinRefusal.None : JoinRefusal.NoGameZone;
    }

    private void RefuseJoin(Wizard wizard, string gameName, string trackText, JoinRefusal refusal) {
        Logger.Warning("{CharId} could not join the pet game {Game} on track {Track}: {Refusal}.",
            Logger.Args(wizard.CharId, gameName, trackText, refusal));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEJOINRSP { Game = gameName, Success = JOIN_FAILED });
        InformGameClient(GetRefusalText(refusal));
    }

    private void TryOpenVisitGame() {
        if (!_attachedToVisitZone || !_visitRoomJoined) {
            return;
        }

        var visit = _visit;
        if (visit.Opened) {
            Logger.Debug("{CharId} came back to the zone of a pet game already played, and is sent back.",
                Logger.Args(GetActiveWizard().CharId));
            ReturnFromVisit();

            return;
        }

        visit.Opened = true;
        if (!TryBeginSession(visit.GameName, visit.TrackIndex, out var session)) {
            ReturnFromVisit();

            return;
        }

        Logger.Debug("{CharId} is in the zone of the pet game {Game}; sending INIT.", Logger.Args(session.Wizard.CharId, session.GameName));
        SendToSocket(PetGameEnergy.BuildTick(session.Wizard));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEINIT {
            Game = session.GameName,
            Data = session.Game.CreateInitData(session),
            MinLevel = PetProgression.TryGetPet(session.Pet, out var behavior, out _) ? behavior.m_level : (byte) 0,
            Track = (byte) session.TrackIndex,
        });
    }

    private bool TryBeginSession(string gameName, int trackIndex, out PetGameSession session) {
        session = null;
        var wizard = GetActiveWizard();
        var refusal = CheckSessionStart(wizard, gameName, trackIndex, out var pet, out var track);
        if (refusal != JoinRefusal.None) {
            RefuseSession(wizard, gameName, trackIndex, refusal);

            return false;
        }

        // A new game closes the end screen of the last one.
        _session = null;
        var newSession = new PetGameSession(wizard, pet, gameName, trackIndex, track, wizard.Zone, PetGameFactory.Create(gameName));
        if (!TryBeginActivity(newSession)) {
            RefuseSession(wizard, gameName, trackIndex, JoinRefusal.InOtherPetActivity);

            return false;
        }

        _session = newSession;
        session = newSession;

        return true;
    }

    private JoinRefusal CheckSessionStart(Wizard wizard, string gameName, int trackIndex, out WizClientObjectItem pet, out PetStatModificationSet track) {
        pet = wizard.GetEquippedPetItem();
        track = PetGameConfig.GetTrackChoice(gameName, trackIndex);
        if (_session is { IsInPlay: true }) {
            return JoinRefusal.GameInPlay;
        }

        if (!PetProgression.TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            return JoinRefusal.NoHatchedPet;
        }

        var game = PetGameConfig.FindGame(gameName);
        if (game is null || game.m_isWebGame || track is null) {
            return JoinRefusal.UnknownGame;
        }

        return PetGameEnergy.CheckCost(wizard, gameName, behavior.m_level) == PetGameEnergyResult.Charged
            ? JoinRefusal.None
            : JoinRefusal.NotEnoughEnergy;
    }

    private void RefuseSession(Wizard wizard, string gameName, int trackIndex, JoinRefusal refusal) {
        Logger.Warning("{CharId} could not start the pet game {Game} on track {Track}: {Refusal}.",
            Logger.Args(wizard.CharId, gameName, trackIndex, refusal));
        InformGameClient(GetRefusalText(refusal));
    }

    private static string GetRefusalText(JoinRefusal refusal)
        => refusal switch {
            JoinRefusal.GameInPlay => GAME_IN_PLAY_TEXT,
            JoinRefusal.NoHatchedPet => NO_HATCHED_PET_TEXT,
            JoinRefusal.InOtherPetActivity => OTHER_PET_ACTIVITY_TEXT,
            JoinRefusal.NotEnoughEnergy => NOT_ENOUGH_ENERGY_TEXT,
            _ => GAME_UNAVAILABLE_TEXT,
        };

    private bool TryChargeEnergy(PetGameSession session) {
        var level = PetProgression.TryGetPet(session.Pet, out var behavior, out _) ? behavior.m_level : 0;
        if (PetGameEnergy.TryCharge(session.Wizard, session.GameName, level, out var cost) != PetGameEnergyResult.Charged) {
            return false;
        }

        session.EnergyCost = cost;
        SendToSocket(PetGameEnergy.BuildTick(session.Wizard));

        return true;
    }

    private static bool TryBeginActivity(PetGameSession session) {
        var wizard = session.Wizard;
        if (PetActivityGuard.TryBegin(wizard, PetActivity.Game, aborted => PetGameEnergy.Refund(aborted, session.EnergyCost))) {
            return true;
        }

        if (!PetActivityGuard.TryGetActivity(wizard, out var activity) || activity != PetActivity.Game) {
            return false;
        }

        // Only this service runs pet games, and it has none in play, so the game left was this service's before a restart.
        PetActivityGuard.Abort(wizard);

        return PetActivityGuard.TryBegin(wizard, PetActivity.Game, aborted => PetGameEnergy.Refund(aborted, session.EnergyCost));
    }

    private void CloseGame(string gameName) {
        if (_session?.IsGame(gameName) == true) {
            EndSession();
        }

        if (_visit?.IsGame(gameName) == true) {
            ReturnFromVisit();
        }
    }

    private void ReturnFromVisit() {
        var visit = _visit;
        _visit = null;
        _attachedToVisitZone = false;
        _visitRoomJoined = false;
        if (visit is null) {
            return;
        }

        var wizard = GetActiveWizard();
        PetGameVisits.End(wizard.CharId);
        Logger.Debug("{CharId} leaves the pet game {Game} for {Zone}.", Logger.Args(wizard.CharId, visit.GameName, visit.ReturnZone));
        Teleport(visit.ReturnZone, doTeleportEffects: false, destinationLocation: visit.ReturnLocation);
    }

    private void FeedSnack(ulong snackTemplateId) {
        var session = _session;
        var result = snackTemplateId <= uint.MaxValue
            ? PetFeeding.Feed(session.Wizard, session.Pet, (uint) snackTemplateId, wholeStack: false)
            : PetFeedResult.None;
        if (!result.Fed) {
            Logger.Debug("{CharId} could not feed snack {Snack} after the pet game {Game}.",
                Logger.Args(session.Wizard.CharId, snackTemplateId, session.GameName));
            SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMESNACKFEEDFAILED());

            return;
        }

        _session = null;
        if (PetFeeding.TryCreateSnackBagUpdate(session.Wizard, result, out var bagUpdate)) {
            SendToSocket(bagUpdate);
        }

        var snack = CoreObjectFactory.GetCoreTemplate((uint) snackTemplateId) as PetSnackItemTemplate;
        var (stats, xp) = PetXpAward.GiveStatMods(SessionActor.ActorRef, GetActiveGameObject(), session.Wizard, session.Pet,
            snack?.m_statModifierSet, PetXpSource.Snack,
            createSummary: (feedStats, feedXp) => new PET_9_PROTOCOL.MSG_PETGAMESNACKFEEDSUCCESS {
                Data = SerializeEndData(session.Score, feedStats.Mods, feedXp.Granted, session.Wins),
            });

        // The snack raised the pet's happiness even when it gave no stats or XP.
        if (stats.Applied == 0 && !xp.Applied && PetBehaviorUpdate.TryCreate(session.Pet, out var update)) {
            SendToSocket(update);
        }
    }

    private void EndSession() {
        var session = _session;
        _session = null;
        if (session is { IsInPlay: true }) {
            PetActivityGuard.End(session.Wizard);
        }
    }

    private void AbortSession(bool sendEnergy) {
        var session = _session;
        _session = null;
        if (session is not { IsInPlay: true } || !PetActivityGuard.Abort(session.Wizard)) {
            return;
        }

        Logger.Debug("{CharId} left the pet game {Game} before it ended.", Logger.Args(session.Wizard.CharId, session.GameName));
        if (sendEnergy) {
            SendToSocket(PetGameEnergy.BuildTick(session.Wizard));
        }
    }

    private static uint GetWins(PetGameSession session)
        => WizardPetCollection.GetGamesWon(session.Wizard.CharId, session.Pet.m_globalID.Full, session.GameName);

    private static ByteString SerializeEndData(float score, PetStatModificationSet mods, uint xp, uint wins) {
        var endData = new PetGameEndData {
            m_Score = score,
            m_statMods = mods,
            m_xpGain = xp,
            m_wins = wins,
        };

        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        if (!serializer.Serialize(endData, END_DATA_PROPERTY_MASK, out var data)) {
            Logger.Error("Could not serialize the end of a pet game.");

            return ByteString.Empty;
        }

        return data;
    }

}
