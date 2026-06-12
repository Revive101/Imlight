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
 * MAGIC LEVELS CONFIGURATION
 * ========================================================================
 * 
 * PURPOSE:
 * Manages configuration and retrieval of magic level information 
 * for player characters and mobs, including experience, health, and 
 * progression details as they are defined in the Root.wad.
 * 
 * USAGE EXAMPLE:
 * // Get level information for a specific magic school
 * var levelInfo = MagicLevelsConfig.GetPlayerLevelInfo(MagicSchool.Fire, 10);
 * 
 * // Get max character level
 * int maxLevel = MagicLevelsConfig.MaxLevel;
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Behaviors;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using System.Linq;

namespace Imlight.CoreLib.Shared.Character;

internal class MagicLevelsConfig : RootSingleResourceSingleton<MagicLevelsConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "MagicXPConfig.xml";

    public static int MaxLevel { get; private set; }

    private static readonly int s_imlightMaxLevel = ConfigurationManager.Settings["Character.MaxLevel"].AsInt();
    private static Dictionary<int, int> s_mobLevelConfig;
    private static Dictionary<string, List<MagicLevelInfo>> s_playerLevelConfig;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<MagicXPConfig>(base.Stream.ToArray(), 1, out var magicXPConfig)) {
            Logger.Error("Failed to load MagicXPConfig.xml");

            return;
        }

        LoadMaxLevel(magicXPConfig);
        LoadMobLevelConfig(magicXPConfig);
        LoadPlayerLevelConfig(magicXPConfig);

        DisposeStream();
    }

    /// <summary>
    /// Gets the rank of a mob at the specified level.
    /// </summary>
    /// <param name="level">The level of the mob.</param>
    /// <returns>The rank of the mob at the specified level. Returns 0 if the rank is not found.</returns>
    internal static int GetMobRankAtLevel(int level) {
        foreach (var kvp in s_mobLevelConfig) {
            if (kvp.Key == level) {
                return kvp.Value;
            }
        }

        return 0;
    }

    /// <summary>
    /// Retrieves the mob level based on the given rank.
    /// </summary>
    /// <param name="rank">The rank of the mob.</param>
    /// <returns>The level of the mob.</returns>
    internal static int GetMobLevelByRank(int rank) {
        if (s_mobLevelConfig.TryGetValue(rank, out var level)) {
            return level;
        }

        return 0;
    }

    /// <summary>
    /// Retrieves the level information for a specific magic school and level.
    /// </summary>
    /// <param name="magicSchool">The magic school.</param>
    /// <param name="level">The level.</param>
    /// <returns>The level information for the specified magic school and level.</returns>
    internal static MagicLevelInfo GetPlayerLevelInfo(MagicSchool magicSchool, int level)
        => GetPlayerLevelInfo(magicSchool.ToString(), level);

    /// <summary>
    /// Retrieves the level information for a specific class and level.
    /// </summary>
    /// <param name="className">The class name of the player.</param>
    /// <param name="level">The level of the player.</param>
    /// <returns>The level information for the specified magic school and level.</returns>
    internal static MagicLevelInfo GetPlayerLevelInfo(string className, int level) {
        if (s_playerLevelConfig.TryGetValue(className, out var levelInfo)) {
            if (level < levelInfo.Count) {
                return levelInfo[level];
            }
        }

        // Fallback: if the class isn't found (e.g., Sun, Star, Moon, Shadow),
        // use the first available class's data so stat recalculation doesn't fail.
        if (s_playerLevelConfig is not null && s_playerLevelConfig.Count > 0) {
            var fallbackLevelInfo = s_playerLevelConfig.Values.First();
            if (level < fallbackLevelInfo.Count) {
                return fallbackLevelInfo[level];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the level of a player based on the experience points.
    /// </summary>
    /// <param name="xp">The experience points.</param>
    /// <returns>The level of the player based on the experience points.</returns>
    internal static byte GetPlayerLevelAtExperience(int xp) {
        if (s_playerLevelConfig is null || s_playerLevelConfig.Count == 0) {
            return 0;
        }

        // All classes need the same amount of XP to level up.
        // We will use the first class to determine the level.
        var levelInfo = s_playerLevelConfig.Values.First();
        for (int i = 0; i < levelInfo.Count; i++) {
            var level = levelInfo[i];
            if (xp < level.m_xpToLevel) {
                return (byte) i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Gets the experience points required to reach a specific level.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <returns>The experience points required to reach the specified level.</returns>
    internal static int GetExperiencePointsAtLevel(int level) {
        if (s_playerLevelConfig is null || s_playerLevelConfig.Count == 0) {
            return 0;
        }

        // All classes need the same amount of XP to level up.
        // We will use the first class to determine the level.
        var levelInfo = s_playerLevelConfig.Values.First();
        if (level > 0 && level < levelInfo.Count) {
            return levelInfo[level - 1].m_xpToLevel;
        }

        return 0;
    }

    private static void LoadMaxLevel(MagicXPConfig magicXPConfig) {
        // If the configuration deems the max level to be lower than the one recorded here,
        // we will use the configuration's max level instead.
        MaxLevel = magicXPConfig.m_maxSchoolLevel;
        if (s_imlightMaxLevel < MaxLevel) {
            MaxLevel = s_imlightMaxLevel;
        }

        Logger.Information("Imlight max level set to {0}", Logger.Args(MaxLevel));
    }

    private static void LoadMobLevelConfig(MagicXPConfig magicXPConfig) {
        s_mobLevelConfig = [];
        var counter = 0;

        foreach (var mobLevel in magicXPConfig.m_levelsConfig) {
            s_mobLevelConfig.Add(mobLevel.m_rank, mobLevel.m_level);
            counter++;
        }

        Logger.Information("Loaded {0} mob level configurations", Logger.Args(counter));
    }

    private static void LoadPlayerLevelConfig(MagicXPConfig magicXPConfig) {
        // There are two lists of data here. One is the `m_levelInfo` which is non-unique and shared
        // between all schools. It details how much mana, power pip chance, and xp is required to level up.
        // However, the `m_classInfo` is the same as `m_levelInfo` but is unique to each school, and details
        // how much base health the wizard has at any given level.
        s_playerLevelConfig = [];
        var allSchoolInfo = magicXPConfig.m_levelInfo;

        var counter = 0;
        foreach (var classInfo in magicXPConfig.m_classInfo) {
            var key = classInfo.m_className;
            var val = classInfo.m_classLevelInfo;

            for (int i = 0; i < val.Count; i++) {
                // Copy over the shared data to the unique data.
                // The only one that is not shared is the health value.
                var levelInfo = val[i];
                levelInfo.m_archmastery = allSchoolInfo[i].m_archmastery;
                levelInfo.m_gold = allSchoolInfo[i].m_gold;
                levelInfo.m_mana = allSchoolInfo[i].m_mana;
                levelInfo.m_petEnergy = allSchoolInfo[i].m_petEnergy;
                levelInfo.m_pipChance = allSchoolInfo[i].m_pipChance;
                levelInfo.m_pipConversionRatingAllSchools = allSchoolInfo[i].m_pipConversionRatingAllSchools;
                levelInfo.m_pipConversionRatingBalance = allSchoolInfo[i].m_pipConversionRatingBalance;
                levelInfo.m_pipConversionRatingDeath = allSchoolInfo[i].m_pipConversionRatingDeath;
                levelInfo.m_pipConversionRatingFire = allSchoolInfo[i].m_pipConversionRatingFire;
                levelInfo.m_pipConversionRatingIce = allSchoolInfo[i].m_pipConversionRatingIce;
                levelInfo.m_pipConversionRatingLife = allSchoolInfo[i].m_pipConversionRatingLife;
                levelInfo.m_pipConversionRatingMyth = allSchoolInfo[i].m_pipConversionRatingMyth;
                levelInfo.m_pipConversionRatingStorm = allSchoolInfo[i].m_pipConversionRatingStorm;
                levelInfo.m_shadowPipRating = allSchoolInfo[i].m_shadowPipRating;
                levelInfo.m_trainingPoints = allSchoolInfo[i].m_trainingPoints;
                levelInfo.m_xpToLevel = allSchoolInfo[i].m_xpToLevel;

                counter++;
            }

            s_playerLevelConfig.Add(key, val);
        }

        Logger.Information("Loaded {0} player level configurations", Logger.Args(counter));
    }

    public void DisposeStream()
        => base.Stream.Dispose();

}