/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Sigils;

internal class SigilFactory : RootDirectoryResourceSingleton<SigilFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "Sigils/";

    private Dictionary<string, SigilTemplate> _combatSigils = [];

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

            _combatSigils.Add(sigil.m_sigilName, sigil);
            counter++;
        }

        Logger.Information("Loaded {0} sigils.", Logger.Args(counter));
    }

    internal static SigilTemplate GetSigilTemplate(string sigilName) {
        if (Instance._combatSigils.TryGetValue(sigilName, out var sigil)) {
            return sigil;
        }

        return null;
    }

    public void DisposeStream() 
        => _combatSigils = null;
    
}
