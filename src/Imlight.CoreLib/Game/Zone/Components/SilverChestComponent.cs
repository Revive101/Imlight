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
 * SILVER CHEST COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Keeps the chest-side state of a chest whose interact option runs a ResMinigame result: which player holds
 * the chest while the game runs, and consuming the chest when the game is played to the end, won or lost.
 * The spawner brings a new one later.
 *
 * USAGE EXAMPLE:
 * Attached by the component registry to every template whose interact options hold a ResMinigame result.
 * The click itself is the result list's: ResMinigameHandler starts the game and names this chest in the
 * game's context.
 *
 * NOTE:
 * One player at a time holds the chest: Idle, InGame, Consumed. The state is world state and is not persisted.
 * The minigame process reports STARTED, RESULT and ABORTED; a STARTED for a chest that another player holds is
 * answered with a refusal and that process pays nothing.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Linq;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class SilverChestComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IComponentFactory {

    private enum ChestState {
        Idle,
        InGame,
        Consumed,
    }

    private ChestState _state = ChestState.Idle;
    private ulong _playerCharId;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate go
            && InteractObjectStateComponent.GetResults(go).OfType<ResMinigame>().Any();

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CHEST_GAME_STARTED))]
    private void ReceiveChestGameStarted(ZONE_102_PROTOCOL.MSG_CHEST_GAME_STARTED message) {
        var heldByPlayer = _state == ChestState.InGame && _playerCharId == message.CharId;
        if (!heldByPlayer && _state != ChestState.Idle) {
            Logger.Warning("A silver chest game of {CharId} started on a chest that is {State}",
                Logger.Args(message.CharId, _state));
            Sender.Tell(new PROCESS_107_PROTOCOL.MSG_CHEST_GAME_REFUSED(), Self);

            return;
        }

        _state = ChestState.InGame;
        _playerCharId = message.CharId;
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CHEST_GAME_RESULT))]
    private void ReceiveChestGameResult(ZONE_102_PROTOCOL.MSG_CHEST_GAME_RESULT message) {
        if (_state != ChestState.InGame || _playerCharId != message.CharId) {
            return;
        }

        _state = ChestState.Consumed;
        Entity.DeleteObject();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CHEST_GAME_ABORTED))]
    private void ReceiveChestGameAborted(ZONE_102_PROTOCOL.MSG_CHEST_GAME_ABORTED message) {
        if (_state != ChestState.InGame || _playerCharId != message.CharId) {
            return;
        }

        _state = ChestState.Idle;
        _playerCharId = 0;
    }

}
