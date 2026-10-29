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
 */
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using System;

namespace Imlight.CoreLib.Game.Results.Handlers;

internal sealed class ResMinigameHandler : BaseResultHandler<ResMinigame> {

    private const float QUERY_WIZARD_TIMEOUT_SECONDS = 5.0f;

    public override bool Execute(IResultContext context) {
        var minigameName = Result.m_minigame;
        if (!Minigames.MinigameConfig.TryGetMinigameByName(minigameName, out var minigame)) {
            if (!Minigames.MinigameConfig.TryGetRandomChestGame(out minigame)) {
                Logger.Error("The minigame result names {Minigame}, which is not a playable game, and no chest game is configured",
                    Logger.Args(Result.m_minigame));

                return false;
            }

            minigameName = minigame.m_name;
        }

        var playerRef = context.GetPlayerRef();
        var queryTimeout = TimeSpan.FromSeconds(QUERY_WIZARD_TIMEOUT_SECONDS);
        var wizard = playerRef
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(), queryTimeout)
            .Result?.Wizard;
        if (wizard is null) {
            Logger.Warning("Result {Result} was skipped: the character could not be read, most likely because the player logged out.",
                Logger.Args(nameof(ResMinigame)));

            return false;
        }

        var playerObject = context.GetPlayerObj();
        var chestTemplateId = context.GetSourceTemplateId();
        var chestGame = new ChestGameContext(
            ChestRef: context.GetSourceRef(),
            ChestTemplateId: chestTemplateId,
            LootTables: ChestLootRoller.ResolveTableNames(Result.m_lootTables, chestTemplateId),
            SuccessScore: Result.m_successScore,
            CharId: wizard.CharId,
            MinigameName: minigameName,
            ReturnZone: wizard.Zone,
            ReturnLocation: Util.GetCompactStringFromVector(playerObject.m_location, playerObject.m_orientation),
            ReturnPrivate: context.IsSourceInstance());
        playerRef.Tell(new SERVICE_101_PROTOCOL.MSG_START_CHEST_MINIGAME { Context = chestGame });

        return true;
    }

}
