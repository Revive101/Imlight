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
 * PET GAME SESSION
 * ========================================================================
 *
 * PURPOSE:
 * One player's pet game from the join to the snack pick: the pet, the game and track, the energy paid,
 * and how far the game has got.
 *
 * USAGE EXAMPLE:
 * var session = new PetGameSession(wizard, pet, gameName, trackIndex, track, zone, PetGameFactory.Create(gameName));
 *
 * NOTE:
 * Lives in the player's PetGamesService.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Linq;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;
using PetGameConfig = Imlight.CoreLib.Game.Pet.Config.PetGameConfig;

namespace Imlight.CoreLib.Game.Pet.Games;

internal enum PetGameSessionState {

    AwaitingReady,
    Playing,
    Results

}

/// <summary>
/// One player's pet game.
/// </summary>
/// <param name="wizard">The player.</param>
/// <param name="pet">The pet that plays.</param>
/// <param name="gameName">The game name as it appears in PetGames.xml.</param>
/// <param name="trackIndex">The index of the track choice.</param>
/// <param name="track">The track choice: its stat changes and score factors.</param>
/// <param name="zone">The zone the game plays in.</param>
/// <param name="game">The play of the game.</param>
internal sealed class PetGameSession(Wizard wizard,
                                     WizClientObjectItem pet,
                                     string gameName,
                                     int trackIndex,
                                     PetStatModificationSet track,
                                     string zone,
                                     IPetGame game) {

    private const PropertyFlags GAME_INFO_PROPERTY_MASK = PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Public;

    /// <summary>
    /// The player.
    /// </summary>
    internal Wizard Wizard { get; } = wizard;

    /// <summary>
    /// The pet that plays.
    /// </summary>
    internal WizClientObjectItem Pet { get; } = pet;

    /// <summary>
    /// The game name as it appears in PetGames.xml.
    /// </summary>
    internal string GameName { get; } = gameName;

    /// <summary>
    /// The index of the track choice.
    /// </summary>
    internal int TrackIndex { get; } = trackIndex;

    /// <summary>
    /// The track choice.
    /// </summary>
    internal PetStatModificationSet Track { get; } = track;

    /// <summary>
    /// The zone the game plays in. Leaving it aborts the game.
    /// </summary>
    internal string Zone { get; } = zone;

    /// <summary>
    /// The play of the game.
    /// </summary>
    internal IPetGame Game { get; } = game;

    /// <summary>
    /// The energy the game was charged, which an abort refunds part of.
    /// </summary>
    internal int EnergyCost { get; set; }

    /// <summary>
    /// How far the game has got.
    /// </summary>
    internal PetGameSessionState State { get; set; } = PetGameSessionState.AwaitingReady;

    /// <summary>
    /// The score the end screen shows, once the game is over.
    /// </summary>
    internal float Score { get; set; }

    /// <summary>
    /// The pet's wins in this game, once the game is over.
    /// </summary>
    internal uint Wins { get; set; }

    /// <summary>
    /// True while the game is not over yet, so leaving it is an abort.
    /// </summary>
    internal bool IsInPlay => State is PetGameSessionState.AwaitingReady or PetGameSessionState.Playing;

    /// <summary>
    /// Checks whether a message names this session's game.
    /// </summary>
    /// <param name="gameName">The game name in the message.</param>
    /// <returns>True if it is this game.</returns>
    internal bool IsGame(string gameName)
        => string.Equals(gameName, GameName, StringComparison.Ordinal);

    /// <summary>
    /// Works out the XP a score is worth on this track: the score times the number of the track's score
    /// factors it reaches.
    /// </summary>
    /// <param name="score">The game's score.</param>
    /// <returns>The XP, before any bonus for the source.</returns>
    internal uint GetScoreXp(float score) {
        // todo: m_gameScoreFactor is a list (such as 0, 10, 20, 40), not one factor. Is it a list of score thresholds?
        if (score <= 0 || float.IsNaN(score)) {
            return 0;
        }

        var factor = Track?.m_gameScoreFactor?.Count(threshold => score >= threshold) ?? 0;

        return (uint) Math.Min(Math.Round((double) score * factor), uint.MaxValue);
    }

    /// <summary>
    /// Serializes the game's own PetGameInfo, which the client's game window loads from MSG_PETGAMEINIT.
    /// </summary>
    /// <returns>The serialized info, empty if the game is not in PetGames.xml.</returns>
    internal ByteString SerializeGameInfo() {
        var info = PetGameConfig.FindGame(GameName);
        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);

        return info is not null && serializer.Serialize(info, GAME_INFO_PROPERTY_MASK, out var data) ? data : ByteString.Empty;
    }

}
