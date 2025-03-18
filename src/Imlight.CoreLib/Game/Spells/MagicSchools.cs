/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Spells;

public class MagicSchools : RootDirectoryResourceSingleton<SpellFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "MagicSchools/";

    private static readonly Dictionary<int, MagicSchoolTemplate> s_magicSchools = new();

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<MagicSchoolTemplate>(fileStream.ToArray(), 1, out var magicSchoolTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(MagicSchoolTemplate)));

                continue;
            }

            if (s_magicSchools.ContainsKey(magicSchoolTemplate.m_schoolIndex)) {
                Logger.Error("Duplicate magic school {0} found in {1}.", 
                    Logger.Args(magicSchoolTemplate.m_schoolName, fileRecord.FileName));
                    
                continue;
            }

            s_magicSchools.Add(magicSchoolTemplate.m_schoolIndex, magicSchoolTemplate);

            counter++;
        }

        Logger.Information("Loaded {0} magic schools.", Logger.Args(counter));
    }

    public static MagicSchoolTemplate GetMagicSchool(int schoolIndex) {
        if (s_magicSchools.TryGetValue(schoolIndex, out var magicSchool)) {
            return magicSchool;
        }

        return null;
    }

    public static MagicSchoolTemplate GetMagicSchool(string schoolName)
        => s_magicSchools.Values.FirstOrDefault(x => x.m_schoolName == schoolName);

    public static MagicSchoolTemplate GetMagicSchool(uint stringHash) {
        foreach (var school in s_magicSchools.Values) {
            var hash = StringHash.Compute(school.m_schoolName);

            if (hash == stringHash) {
                return school;
            }
        }

        return null;
    }

    public static uint GetMaxMagicSchoolIndex() => (uint) s_magicSchools.Keys.Max();

    public void DisposeStream() {
        foreach (var stream in base.Files.Values) {
            stream.Dispose();
        }
    }
    
}
