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
 * CINEMATIC ACTOR FACTORY
 * ========================================================================
 *
 * PURPOSE:
 * Loads and caches the cinematic actor templates (WizCinematicActorTemplate) as they are defined in the
 * Root.wad, keyed by the m_cinematicName a zone trigger's ResCinematicActor names.
 *
 * USAGE EXAMPLE:
 * var template = CinematicActorFactory.GetTemplate("Cinematic_SpiritActor");
 *
 * NOTE:
 * The directory name is matched as a prefix, so it covers both ObjectData/Cinematic_*.xml and the
 * ObjectData/Cinematics/ subdirectory. Templates are keyed by their own m_cinematicName, not the file
 * name: one file name differs from the name its trigger uses.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/27/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Cinematics;

internal class CinematicActorFactory : RootDirectoryResourceSingleton<CinematicActorFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "ObjectData/Cinematic";

    private static readonly Dictionary<string, WizCinematicActorTemplate> s_cinematicActorTemplates
        = new(StringComparer.OrdinalIgnoreCase);

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var count = 0;

        foreach (var (fileRecord, fileStream) in base.Files) {
            if (!serializer.Deserialize<CoreTemplate>(fileStream?.ToArray(), out var template)) {
                Logger.Error("Could not deserialize {0} as {1}",
                    Logger.Args(fileRecord.FileName, nameof(CoreTemplate)));

                continue;
            }

            // The same files hold plain cinematic definitions; only actor templates are played by triggers.
            if (template is not WizCinematicActorTemplate cinematicActor
                || string.IsNullOrEmpty(cinematicActor.m_cinematicName)) {
                continue;
            }

            if (s_cinematicActorTemplates.TryAdd(cinematicActor.m_cinematicName, cinematicActor)) {
                count++;
            }
        }

        Logger.Information("Loaded {0} cinematic actor templates.",
            Logger.Args(count));
    }

    /// <summary>
    /// Retrieves a cinematic actor template by its cinematic name.
    /// </summary>
    /// <param name="cinematicName">The name a ResCinematicActor gives (m_cinematicName).</param>
    /// <returns>The template, or null if there is none by that name.</returns>
    internal static WizCinematicActorTemplate GetTemplate(string cinematicName) {
        if (!string.IsNullOrEmpty(cinematicName)
            && s_cinematicActorTemplates.TryGetValue(cinematicName, out var template)) {
            return template;
        }

        return null;
    }

    public void DisposeStream()
        => s_cinematicActorTemplates.Clear();

}
