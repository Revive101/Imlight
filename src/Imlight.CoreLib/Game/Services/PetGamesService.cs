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
 * The kiosk window sends MSG_PETGAMEJOIN {Game, Track}; the client answers MSG_PETGAMEINIT with
 * MSG_PETGAMEREADY, and the game plays from MSG_PETGAMESTART to MSG_PETGAMEEND.
 *
 * NOTE:
 * HatchingService also handles MSG_PETGAMEREADY, MSG_PETGAMEDATA and MSG_PETGAMEENDING, and the session
 * gives every service a copy. Each service takes only the messages of the window it opened: this one acts
 * on a message only while it has a session, and on DATA and ENDING only when they name that session's game.
 * Quitting a game gives no results and no refund. Leaving the zone or the session aborts the game, which
 * refunds part of its energy.
 *
 * TODO:
 * - Does each game need its own zone? The older fork played its dance game in a private game zone.
 * - Should a game time out when the client never sends READY or never finishes?
 *
 * Created by: Jay
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

    private const byte QUIT_DATA_COMMAND = 3;
    private const byte SNACK_PICK_DATA_COMMAND = 4;
    private const byte JOIN_SUCCEEDED = 1;
    private const byte JOIN_FAILED = 0;
    private const int SKIPPED_GAME_TRACK_INDEX = 0;
    private const PropertyFlags END_DATA_PROPERTY_MASK = PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Public;
    private const string NO_HATCHED_PET_TEXT = "You need to equip a hatched pet to play pet games.";
    private const string NOT_ENOUGH_ENERGY_TEXT = "You do not have enough energy to play this game.";

    private PetGameSession _session;

    PetGameSession IPetGameHost.Session => _session;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PetGamesService(parentActor));

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEJOIN))]
    private void ReceivePetGameJoin(PET_9_PROTOCOL.MSG_PETGAMEJOIN message) {
        var gameName = (string) message.Game;
        if (!int.TryParse((string) message.Track, NumberStyles.Integer, CultureInfo.InvariantCulture, out var trackIndex)
            || !TryBeginSession(gameName, trackIndex, out var session)) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEJOINRSP { Game = gameName, Success = JOIN_FAILED });

            return;
        }

        SendToSocket(PetGameEnergy.BuildTick(session.Wizard));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEJOINRSP { Game = gameName, Success = JOIN_SUCCEEDED });
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEINIT {
            Game = gameName,
            Data = session.Game.CreateInitData(session),
            MinLevel = PetProgression.TryGetPet(session.Pet, out var behavior, out _) ? behavior.m_level : (byte) 0,
            Track = (byte) trackIndex,
        });
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEREADY))]
    private void ReceivePetGameReady(PET_9_PROTOCOL.MSG_PETGAMEREADY message) {
        if (_session is not { State: PetGameSessionState.AwaitingReady } session) {
            return;
        }

        session.State = PetGameSessionState.Playing;
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
    private void ReceivePetGameEnding(PET_9_PROTOCOL.MSG_PETGAMEENDING message) {
        if (_session is null || !_session.IsGame(message.Game)) {
            return;
        }

        EndSession();
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIPFEED))]
    private void ReceivePetGameSkipFeed(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIPFEED message) {
        if (_session is null || !_session.IsGame(message.Game)) {
            return;
        }

        EndSession();
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIP))]
    private void ReceivePetGameSkip(WIZARD2_53_PROTOCOL.MSG_PETGAMESKIP message) {
        // todo: the skip names no track, so it plays the first one. Which track does live use?
        if (!TryBeginSession(message.Game, SKIPPED_GAME_TRACK_INDEX, out var session)) {
            return;
        }

        SendToSocket(PetGameEnergy.BuildTick(session.Wizard));
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

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        if (_session is null || string.Equals(_session.Wizard.Zone, _session.Zone, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        AbortSession(sendEnergy: true);
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

        PetXpAward.GiveStatMods(SessionActor.ActorRef, GetActiveGameObject(), session.Wizard, session.Pet, session.Track, PetXpSource.Game,
            session.GetScoreXp(outcome.Score),
            (stats, xp) => new PET_9_PROTOCOL.MSG_PETGAMEEND {
                Game = session.GameName,
                Data = SerializeEndData(session.Score, stats.Mods, xp.Granted, session.Wins),
            });
    }

    private bool TryBeginSession(string gameName, int trackIndex, out PetGameSession session) {
        session = null;
        if (_session is { IsInPlay: true }) {
            return false;
        }

        // A new game closes the end screen of the last one.
        _session = null;
        var wizard = GetActiveWizard();
        var pet = wizard.GetEquippedPetItem();
        if (!PetProgression.TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            InformGameClient(NO_HATCHED_PET_TEXT);

            return false;
        }

        var game = PetGameConfig.FindGame(gameName);
        var track = PetGameConfig.GetTrackChoice(gameName, trackIndex);
        if (game is null || game.m_isWebGame || track is null) {
            Logger.Warning("{CharId} asked to join the pet game {Game} on track {Track}, which does not exist.",
                Logger.Args(wizard.CharId, gameName, trackIndex));

            return false;
        }

        var newSession = new PetGameSession(wizard, pet, gameName, trackIndex, track, wizard.Zone, PetGameFactory.Create(gameName));
        if (!TryBeginActivity(newSession)) {
            return false;
        }

        if (PetGameEnergy.TryCharge(wizard, gameName, behavior.m_level, out var cost) != PetGameEnergyResult.Charged) {
            PetActivityGuard.End(wizard);
            InformGameClient(NOT_ENOUGH_ENERGY_TEXT);

            return false;
        }

        newSession.EnergyCost = cost;
        _session = newSession;
        session = newSession;

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

    private void FeedSnack(ulong snackTemplateId) {
        var session = _session;
        var result = snackTemplateId <= uint.MaxValue
            ? PetFeeding.Feed(session.Wizard, session.Pet, (uint) snackTemplateId, wholeStack: false)
            : PetFeedResult.None;
        if (!result.Fed) {
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
