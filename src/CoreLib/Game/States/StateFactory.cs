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

namespace Imlight.CoreLib.Game.States;

internal class StateFactory : RootDirectoryResourceSingleton<StateFactory>, IMemoryStreamDisposable {
    protected override string DirectoryName => "StateData/";

    private readonly Dictionary<string, ObjStateSet> _objectStateSets = new();

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            var set = serializer.OpenClass<ObjStateSet>(fileStream);
            if (set is null) {
                Logger.Error("Could not deserialize {0} as {1}", Logger.Args(fileRecord.FileName, nameof(ObjStateSet)));
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
