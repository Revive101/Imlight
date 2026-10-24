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
 * SPELL CINEMATICS MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and retrieval of spell cinematic templates
 * with methods to extract timing information for different spell stages
 * as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var castTime = SpellCinematics.GetCastTime(spellBaseName);
 * var playTime = SpellCinematics.GetSpellPlayTime(spellBaseName);
 * 
 * NOTE:
 * A stage a spell's template lacks is read from the fallback template, as the
 * client's cinematic manager does when it builds a combat cinematic.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Spells;

/// <summary>
/// Loads the cinematic templates and reads the stage durations the client plays for a combat action.
/// </summary>
internal class SpellCinematics : RootDirectoryResourceSingleton<SpellCinematics>, IMemoryStreamDisposable {

    private const string FALLBACK_CINEMATIC_NAME = "Master";
    private const string TURN_INDICATOR_STAGE = "TurnIndicator";
    private const string FOCUS_STAGE = "Focus";
    private const string STARTING_STAGE = "Starting";
    private const string TARGET_STAGE = "Target";
    private const string CASTING_STAGE = "Casting";
    private const string MISS_STAGE = "Miss";
    private const string SUMMON_STAGE = "Summon";
    private const string ACT_STAGE_PREFIX = "Act";
    private const string AREA_START_STAGE_PREFIX = "StartAOE";
    private const string AREA_STOP_STAGE_PREFIX = "StopAOE";
    private const string RELEASE_STAGE = "Release";
    private const string DEATH_STAGE = "Death";
    private const string CRITICAL_HIT_STAGE = "CriticalHit";
    private const string ACTIVATE_HANGING_STAGE = "ActivateHanging";
    private const string STUN_RESISTED_STAGE = "StunResisted";
    private const string STUN_IMMUNE_STAGE = "StunImmune";
    private const string ADD_GLOBAL_STAGE = "AddGlobal";
    private const string GLOBAL_REMOVED_STAGE = "GlobalEffectRemoved";

    protected override string DirectoryName => "Cinematics";

    private static readonly Dictionary<string, CinematicTemplate> s_cinematicTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<CinematicTemplate>(fileStream?.ToArray(), 1, out var cinematicTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(CinematicTemplate)));

                continue;
            }

            // Templates can share a name; the one whose file carries that name is the one the spell plays.
            var name = cinematicTemplate.m_name.ToString();
            var fileCarriesName = Path.GetFileNameWithoutExtension(fileRecord.FileName) == name;
            if (s_cinematicTemplates.ContainsKey(name) && !fileCarriesName) {
                continue;
            }

            s_cinematicTemplates[name] = cinematicTemplate;
        }

        Logger.Information("Loaded {0} cinematic templates.", 
            Logger.Args(s_cinematicTemplates.Count));
    }

    /// <summary>
    /// Retrieves a cinematic template based on its name.
    /// </summary>
    /// <param name="name">The name of the cinematic.</param>
    /// <returns>The <see cref="CinematicTemplate"/>, or null when there is none.</returns>
    internal static CinematicTemplate GetCinematicTemplate(string name) {
        if (name is not null && s_cinematicTemplates.TryGetValue(name, out var cinematicTemplate)) {
            return cinematicTemplate;
        }

        return null;
    }

    /// <summary>
    /// Reads a stage's duration from a cinematic, falling back to the shared template when the cinematic lacks it.
    /// </summary>
    /// <param name="cinematicName">The cinematic's name, which is the spell's base name.</param>
    /// <param name="stageName">The stage's name.</param>
    /// <returns>The stage's duration in seconds, or 0 when neither template has it.</returns>
    internal static float GetStageDuration(string cinematicName, string stageName) {
        var stage = FindStage(GetCinematicTemplate(cinematicName), stageName)
                    ?? FindStage(GetCinematicTemplate(FALLBACK_CINEMATIC_NAME), stageName);

        return stage?.m_duration ?? 0.0f;
    }

    /// <summary>
    /// The time the client takes to hand a turn to a participant who passes.
    /// </summary>
    /// <returns>The pass time in seconds.</returns>
    internal static float GetPassTime()
        => GetStageDuration(FALLBACK_CINEMATIC_NAME, TURN_INDICATOR_STAGE)
           + GetStageDuration(FALLBACK_CINEMATIC_NAME, FOCUS_STAGE);

    /// <summary>
    /// The time the client takes to turn to a caster and play the cast, before the spell's own stages.
    /// Only a pass plays the focus stage.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The cast time in seconds.</returns>
    internal static float GetCastTime(string cinematicName)
        => GetStageDuration(cinematicName, TURN_INDICATOR_STAGE)
           + GetStageDuration(cinematicName, STARTING_STAGE)
           + GetStageDuration(cinematicName, TARGET_STAGE)
           + GetStageDuration(cinematicName, CASTING_STAGE);

    /// <summary>
    /// The time a fizzled cast takes: the cast, then the miss.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The fizzle time in seconds.</returns>
    internal static float GetFizzleTime(string cinematicName)
        => GetCastTime(cinematicName) + GetStageDuration(cinematicName, MISS_STAGE);

    /// <summary>
    /// The time a spell's own stages take once it lands: the summon, then either its area stages or one act,
    /// then the release. Hanging effects, crits and deaths add their own stages on top.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The play time in seconds.</returns>
    internal static float GetSpellPlayTime(string cinematicName) {
        var summon = GetStageDuration(cinematicName, SUMMON_STAGE);
        var release = GetStageDuration(cinematicName, RELEASE_STAGE);
        var stages = GetCinematicTemplate(cinematicName)?.m_stages;
        if (stages is null) {
            return summon + release;
        }

        // An area spell plays its acts inside start and stop stages that carry the time; others play one act.
        var areaStages = stages.Where(x => x is not null && IsAreaStage(x.m_name.ToString())).ToList();
        if (areaStages.Count > 0) {
            return summon + areaStages.Sum(x => x.m_duration) + release;
        }

        var act = stages.FirstOrDefault(x => x is not null && IsActStage(x.m_name.ToString()));

        return summon + (act?.m_duration ?? 0.0f) + release;
    }

    /// <summary>
    /// The time the client plays a participant's death.
    /// </summary>
    /// <returns>The death time in seconds.</returns>
    internal static float GetDeathTime()
        => GetStageDuration(FALLBACK_CINEMATIC_NAME, DEATH_STAGE);

    /// <summary>
    /// The time the client plays a critical hit, once for an action however many targets it crits.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The critical hit time in seconds.</returns>
    internal static float GetCriticalHitTime(string cinematicName)
        => GetStageDuration(cinematicName, CRITICAL_HIT_STAGE);

    /// <summary>
    /// The time the client plays one over-time tick.
    /// </summary>
    /// <returns>The tick time in seconds.</returns>
    internal static float GetOverTimeTickTime()
        => GetStageDuration(FALLBACK_CINEMATIC_NAME, ACTIVATE_HANGING_STAGE);

    /// <summary>
    /// The time the client plays a target resisting a stun.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The resist time in seconds.</returns>
    internal static float GetStunResistedTime(string cinematicName)
        => GetStageDuration(cinematicName, STUN_RESISTED_STAGE);

    /// <summary>
    /// The time the client plays a target that is immune to a stun.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <returns>The immunity time in seconds.</returns>
    internal static float GetStunImmuneTime(string cinematicName)
        => GetStageDuration(cinematicName, STUN_IMMUNE_STAGE);

    /// <summary>
    /// The time the client plays a global effect entering the battlefield, and the one it replaces leaving.
    /// </summary>
    /// <param name="cinematicName">The spell's base name.</param>
    /// <param name="replacesGlobal">Whether a global effect was already in play.</param>
    /// <returns>The global effect time in seconds.</returns>
    internal static float GetGlobalEffectTime(string cinematicName, bool replacesGlobal) {
        var added = GetStageDuration(cinematicName, ADD_GLOBAL_STAGE);
        if (!replacesGlobal) {
            return added;
        }

        return added + GetStageDuration(cinematicName, GLOBAL_REMOVED_STAGE);
    }

    private static CinematicStageTemplate FindStage(CinematicTemplate template, string stageName)
        => template?.m_stages?.FirstOrDefault(x => x?.m_name.ToString() == stageName);

    private static bool IsAreaStage(string stageName)
        => stageName.StartsWith(AREA_START_STAGE_PREFIX) || stageName.StartsWith(AREA_STOP_STAGE_PREFIX);

    private static bool IsActStage(string stageName)
        => stageName.StartsWith(ACT_STAGE_PREFIX)
           && stageName[ACT_STAGE_PREFIX.Length..].Trim().All(char.IsDigit);

    public void DisposeStream() 
        => s_cinematicTemplates.Clear();

}
