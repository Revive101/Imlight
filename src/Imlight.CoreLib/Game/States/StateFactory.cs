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
