/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Sigils;

internal class SigilFactory : RootDirectoryResourceSingleton<SigilFactory>, IMemoryStreamDisposable {

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

    public void DisposeStream() 
        => s_combatSigils = null;
    
}
