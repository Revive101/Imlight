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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/08/2026
 */

using Imlight.CoreLib.Game.WizBang;
using Imlight.PluginApi.Npcs;

namespace Imlight.CoreLib.Plugins;

internal static class PluginWizBang {

    /// <summary>
    /// The game's wizbang for the plugin API's <see cref="NpcWizBang"/>.
    /// </summary>
    public static WizBangs ToWizBang(this NpcWizBang wizBang) => wizBang switch {
        NpcWizBang.StartQuest         => WizBangs.StartQuest,
        NpcWizBang.UnfinishedQuest    => WizBangs.UnfinishedQuest,
        NpcWizBang.CompleteQuestGoal  => WizBangs.CompleteQuestGoal,
        NpcWizBang.DailyQuest         => WizBangs.DailyQuest,
        NpcWizBang.Training           => WizBangs.Training,
        NpcWizBang.Shopping           => WizBangs.Shopping,
        NpcWizBang.Banking            => WizBangs.Banking,
        NpcWizBang.Registrar          => WizBangs.Registrar,
        NpcWizBang.LeaderBoard        => WizBangs.LeaderBoard,
        NpcWizBang.CastleTours        => WizBangs.CastleTours,
        NpcWizBang.SellFish           => WizBangs.SellFish,
        NpcWizBang.WhirlyBurly        => WizBangs.WhirlyBurly,
        NpcWizBang.RateMyStitch       => WizBangs.RateMyStitch,
        NpcWizBang.PvPLobby           => WizBangs.PvPLobby,
        NpcWizBang.GuildMuseumCurator => WizBangs.GuildMuseumCurator,
        NpcWizBang.MagicWeavingTome   => WizBangs.MagicWeavingTome,
        NpcWizBang.HousingHallOfFame  => WizBangs.HousingHallOfFame,
        NpcWizBang.NumberZero         => WizBangs.NumberZero,
        NpcWizBang.NumberOne          => WizBangs.NumberOne,
        NpcWizBang.NumberTwo          => WizBangs.NumberTwo,
        NpcWizBang.NumberThree        => WizBangs.NumberThree,
        NpcWizBang.NumberFour         => WizBangs.NumberFour,
        NpcWizBang.NumberFive         => WizBangs.NumberFive,
        NpcWizBang.NumberSix          => WizBangs.NumberSix,
        NpcWizBang.NumberSeven        => WizBangs.NumberSeven,
        NpcWizBang.NumberEight        => WizBangs.NumberEight,
        NpcWizBang.NumberNine         => WizBangs.NumberNine,
        NpcWizBang.NumberTen          => WizBangs.NumberTen,
        NpcWizBang.NumberEleven       => WizBangs.NumberEleven,
        NpcWizBang.NumberTwelve       => WizBangs.NumberTwelve,
        NpcWizBang.NumberThirteen     => WizBangs.NumberThirteen,
        NpcWizBang.NumberFourteen     => WizBangs.NumberFourteen,
        NpcWizBang.NumberFifteen      => WizBangs.NumberFifteen,
        _                             => WizBangs.None,
    };

}
