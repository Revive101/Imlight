/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Sigils;

internal class SigilFactory : RootDirectoryResourceSingleton<SigilFactory>, IMemoryStreamDisposable {
    protected override string DirectoryName => "Sigils/";

    private readonly Dictionary<uint, CombatSigilTemplate> _combatSigils = new();

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            var sigil = serializer.OpenClass<CombatSigilTemplate>(fileStream);
            if (sigil is null) {
                Logger.Error("Could not deserialize {0} as {1}", Logger.Args(fileRecord.FileName, nameof(CombatSigilTemplate)));
                continue;
            }

            // todo: add the rest of the sigil types
            uint templateId = 0;
            switch (sigil.m_sigilName) {
                case "CombatSigil8Actor":
                    templateId = 560;
                    break;
            }

            _combatSigils.Add(templateId, sigil);
            counter++;
        }

        Logger.Information("Loaded {0} combat sigils.", Logger.Args(counter));
    }

    internal static CombatSigilTemplate GetSigilTemplate(uint templateId) {
        if (Instance._combatSigils.TryGetValue(templateId, out var sigil)) {
            return sigil;
        }

        return null;
    }

    public void DisposeStream() {
        foreach (var streams in base.Files.Values) {
            streams.Dispose();
        }
    }
}
