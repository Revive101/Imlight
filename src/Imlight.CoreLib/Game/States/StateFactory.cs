/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * STATE SET MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and retrieval of object state sets
 * from resource files as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var stateSet = StateFactory.GetStateSet("Player");
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

namespace Imlight.CoreLib.Game.States;

internal class StateFactory : RootDirectoryResourceSingleton<StateFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "StateData/";

    private static readonly Dictionary<string, ObjStateSet> s_objectStateSets = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<ObjStateSet>(fileStream?.ToArray(), 1, out var set)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(ObjStateSet)));

                continue;
            }

            s_objectStateSets.Add(set.m_stateSetName, set);
            counter++;
        }

        Logger.Information("Loaded {0} state sets.", 
            Logger.Args(counter));
    }

    /// <summary>
    /// Get the state set by name.
    /// </summary>  
    /// <param name="setName">The name of the state set.</param>
    /// <returns>The state set.</returns>
    internal static ObjStateSet GetStateSet(string setName) {
        if (s_objectStateSets.TryGetValue(setName, out var set)) {
            return set;
        }

        return null;
    }

    public void DisposeStream() 
        => s_objectStateSets.Clear();
    
}
