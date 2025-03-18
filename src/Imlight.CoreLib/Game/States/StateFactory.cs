/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.States;

internal class StateFactory : RootDirectoryResourceSingleton<StateFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "StateData/";

    private readonly Dictionary<string, ObjStateSet> _objectStateSets = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<ObjStateSet>(fileStream.ToArray(), 1, out var set)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(ObjStateSet)));

                continue;
            }

            _objectStateSets.Add(set.m_stateSetName, set);
            counter++;
        }

        Logger.Information("Loaded {0} state sets.", Logger.Args(counter));
    }

    internal static ObjStateSet GetStateSet(string setName) {
        if (Instance._objectStateSets.TryGetValue(setName, out var set)) {
            return set;
        }

        return null;
    }

    public void DisposeStream() {
        foreach (var streams in base.Files.Values) {
            streams.Dispose();
        }
    }
    
}
