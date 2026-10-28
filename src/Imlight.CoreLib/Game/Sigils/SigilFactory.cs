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
 * SIGIL TEMPLATE MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and retrieval of sigil templates 
 * from resource files as they are defined in the Root.wad.
 * 
 * USAGE EXAMPLE:
 * var sigilTemplate = SigilFactory.GetSigilTemplate("Combat8SigilActor");
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
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Sigils;

internal class SigilFactory : RootDirectoryResourceSingleton<SigilFactory>, IMemoryStreamDisposable {

    internal const string MONSTER_CIRCLE_LOCATION_TYPE = "MonsterCircle";

    protected override string DirectoryName => "Sigils/";

    private static Dictionary<string, SigilTemplate> s_combatSigils = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<SigilTemplate>(fileStream?.ToArray(), 1, out var sigil)) {
                Logger.Error("Failed to deserialize sigil {0}.", 
                    Logger.Args(fileRecord));

                continue;
            }

            s_combatSigils.Add(sigil.m_sigilName, sigil);
            counter++;
        }

        Logger.Information("Loaded {0} sigils.", 
            Logger.Args(counter));
    }

    /// <summary>
    /// Get a sigil template by name.
    /// </summary>
    /// <param name="sigilName">The name of the sigil.</param>
    /// <returns>The sigil template.</returns>
    internal static SigilTemplate GetSigilTemplate(string sigilName) {
        if (s_combatSigils.TryGetValue(sigilName, out var sigil)) {
            return sigil;
        }

        return null;
    }

    /// <summary>
    /// Gets the circles of a sigil template that a player can stand in.
    /// </summary>
    /// <param name="sigilType">The name of the sigil.</param>
    /// <returns>The player circles, empty if the template is unknown.</returns>
    internal static List<SigilSubCircle> GetPlayerCircles(string sigilType)
        => [.. GetSigilTemplate(sigilType)?.m_subCircles?.Where(circle => circle is not null && circle.m_locationType != MONSTER_CIRCLE_LOCATION_TYPE) ?? []];

    public void DisposeStream() 
        => s_combatSigils = null;
    
}
