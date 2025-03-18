/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Spells;

internal class SpellCinematics : RootDirectoryResourceSingleton<SpellCinematics>, IMemoryStreamDisposable {

    private const float HANGING_EFFECT_ADD_TIME = 1.0f;

    protected override string DirectoryName => "Cinematics";

    private readonly Dictionary<string, CinematicTemplate> _cinematicTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<CinematicTemplate>(fileStream?.ToArray(), 1, out var cinematicTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(CinematicTemplate)));

                continue;
            }

            // SKip if the template is already in the dictionary.
            if (_cinematicTemplates.ContainsKey(cinematicTemplate.m_name)) {
                continue;
            }

            _cinematicTemplates.Add(cinematicTemplate.m_name, cinematicTemplate);
            counter++;
        }

        Logger.Information("Loaded {0} cinematic templates.", Logger.Args(counter));
    }

    /// <summary>
    /// Retrieves a cinematic template based on its name.
    /// </summary>
    /// <param name="name">The name of the spell.</param>
    /// <returns>The <see cref="CinematicTemplate"/> of the spell. </returns>
    public CinematicTemplate GetCinematicTemplate(string name) {
        if (_cinematicTemplates.TryGetValue(name, out var cinematicTemplate)) {
            return cinematicTemplate;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the summon time of a spell based on its name.
    /// </summary>
    /// <param name="name">The name of the spell.</param>
    /// <returns>The summon time of the spell.</returns>
    public float GetSpellSummonTime(string name) {
        var cinematicTemplate = GetCinematicTemplate(name);
        if (cinematicTemplate is null) {
            return 0.0f;
        }

        // Search the acts of the template to find type `SummonCinematicStageTemplate`.
        foreach (var act in cinematicTemplate.m_stages) {
            if (act is SummonCinematicStageTemplate summonCinematicStageTemplate) {
                return summonCinematicStageTemplate.m_duration;
            }
        }

        return 0.0f;
    }

    /// <summary>
    /// Retrieves the duration of a spell's cinematic act.
    /// </summary>
    /// <param name="name">The name of the spell.</param>
    /// <returns>The duration of the spell's cinematic act.</returns>
    public float GetSpellActTime(string name) {
        var cinematicTemplate = GetCinematicTemplate(name);
        if (cinematicTemplate is null) {
            return 0.0f;
        }

        // Search the acts of the template to find type `ActCinematicStageTemplate`.
        // We don't need to check for a certain act because all of them have the same duration anyways.
        foreach (var act in cinematicTemplate.m_stages) {
            if (act is ActCinematicStageTemplate actCinematicStageTemplate) {
                return actCinematicStageTemplate.m_duration;
            }
            else if (act.m_name == "Act") {
                return act.m_duration;
            }
        }

        return 0.0f;
    }

    /// <summary>
    /// Retrieves the casting time of a spell based on its name.
    /// </summary>
    /// <param name="name">The name of the spell.</param>
    /// <returns>The casting time of the spell.</returns>
    public float GetSpellCastingTime(string name) {
        var cinematicTemplate = GetCinematicTemplate(name);
        if (cinematicTemplate is null) {
            return 0.0f;
        }

        // Search the acts of the template to find type `CastingCinematicStageTemplate`.
        foreach (var act in cinematicTemplate.m_stages) {
            if (act.m_name == "Casting") {
                return act.m_duration;
            }
        }

        return 0.0f;
    }

    /// <summary>
    /// Calculates the total time of a spell's cinematic based on its name.
    /// </summary>
    /// <param name="name">The name of the spell.</param>
    /// <returns>The total time of the spell's cinematic.</returns>
    public float GetSpellTotalTime(string name) {
        var cinematicTemplate = GetCinematicTemplate(name);
        if (cinematicTemplate is null) {
            return 0.0f;
        }

        float totalTime = 0.0f;
        foreach (var act in cinematicTemplate.m_stages) {
            // Add 1 second to the total time if the act is a hanging effect.
            if (act.m_name.ToString().Contains("AddHanging")) {
                totalTime += HANGING_EFFECT_ADD_TIME;
                continue;
            }

            totalTime += act.m_duration;
        }

        return totalTime;
    }

    public void DisposeStream() 
        => _cinematicTemplates.Clear();

}
