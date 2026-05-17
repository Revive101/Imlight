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
 * MAGIC SCHOOLS MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and retrieval of magic school templates
 * from resource files as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var magicSchool = MagicSchools.GetMagicSchool(schoolIndex);
 * var magicSchoolByName = MagicSchools.GetMagicSchool("Fire");
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
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Spells;

/// <summary>
/// Manages the loading, caching, and retrieval of magic school templates.
/// </summary>
internal class MagicSchools : RootDirectoryResourceSingleton<SpellFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "MagicSchools/";

    private static readonly Dictionary<int, MagicSchoolTemplate> s_magicSchools = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<MagicSchoolTemplate>(fileStream?.ToArray(), 1, out var magicSchoolTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(MagicSchoolTemplate)));

                continue;
            }

            // The game client uses a 1-based index for magic schools for templates. However,
            // when we send canonical data, it expects it as 0-based.
            magicSchoolTemplate.m_schoolIndex--;

            if (s_magicSchools.ContainsKey(magicSchoolTemplate.m_schoolIndex)) {
                Logger.Error("Duplicate magic school {0} found in {1}.", 
                    Logger.Args(magicSchoolTemplate.m_schoolName, fileRecord.FileName));
                    
                continue;
            }

            s_magicSchools.Add(magicSchoolTemplate.m_schoolIndex, magicSchoolTemplate);

            counter++;
        }

        Logger.Information("Loaded {0} magic school configurations.", 
            Logger.Args(counter));
    }

    /// <summary>
    /// Get the magic school template by index.
    /// </summary>
    /// <param name="schoolIndex">The index of the magic school.</param>
    /// <returns>The magic school template.</returns>
    internal static MagicSchoolTemplate GetMagicSchool(int schoolIndex) {
        if (s_magicSchools.TryGetValue(schoolIndex, out var magicSchool)) {
            return magicSchool;
        }

        return null;
    }

    /// <summary>
    /// Get the magic school template by name.
    /// </summary>
    /// <param name="schoolName">The name of the magic school.</param>
    /// <returns>The magic school template.</returns>
    internal static MagicSchoolTemplate GetMagicSchool(string schoolName)
        => s_magicSchools.Values.FirstOrDefault(x => x.m_schoolName == schoolName);

    /// <summary>
    /// Get the magic school template by string hash.
    /// </summary>
    /// <param name="stringHash">The string hash of the magic school.</param>
    /// <returns>The magic school template.</returns>
    internal static MagicSchoolTemplate GetMagicSchool(uint stringHash) {
        foreach (var school in s_magicSchools.Values) {
            var hash = StringHash.Compute(school.m_schoolName);

            if (hash == stringHash) {
                return school;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the index of the highest magic school defined.
    /// </summary>
    /// <returns>The index of the highest magic school.</returns>
    internal static uint GetMaxMagicSchoolIndex()
        => (uint) s_magicSchools.Keys.Max();

    public void DisposeStream() 
        => s_magicSchools.Clear();
    
}
