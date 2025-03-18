/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Core;

internal sealed class ZoneLoader : ReceiveProtocolDispatcher {

    private const string ZONE_DATA_FILE_NAME = "gamedata.bin";
    private const string SPAWN_DATA_FILE_NAME = "spawnData.xml";
    private const string PATH_DATA_FILE_NAME = "pathData.xml";
    private const string NODE_DATA_FILE_NAME = "pathNodeData.bin";
    private const string VOLUME_DATA_FILE_NAME = "volumes.xml";
    private const string TRIGGER_DATA_FILE_NAME = "triggers.xml";

    private Archive _wad;
    private readonly System.Diagnostics.Stopwatch _benchmarkTimer = new();

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN))]
    private void ReceiveZoneBeginLoad(ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN message) {
        _benchmarkTimer.Restart();

        if (!ResourceManager.TryLoadArchive(message.ZonePath, out _wad)) {
            var failureMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                Error = true,
                ErrorMessage = $"Failed to load zone archive {message.ZonePath}"
            };

            Sender.Tell(failureMsg);
        }

        try {
            // Load zone data
            var zoneData = LoadZoneData();
            LogLoadingStep("zone data");

            // Load spawn data
            var spawnData = LoadSpawnData();
            LogLoadingStep("spawn data");

            // Load path data
            var pathData = LoadPathData();
            LogLoadingStep("path data");

            // Load node data
            var nodeData = LoadNodeData();
            LogLoadingStep("node data");

            // Load volume data
            var volumeData = LoadVolumeData();
            LogLoadingStep("volume data");

            // Load trigger data
            var triggerData = LoadTriggerData();
            LogLoadingStep("trigger data");

            // Send completion message with all loaded data
            var completionMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                ZoneData = zoneData,
                SpawnData = spawnData,
                PathData = pathData,
                NodeData = nodeData,
                TriggerData = triggerData,
                VolumeData = volumeData
            };

            Sender.Tell(completionMsg);
            _benchmarkTimer.Stop();
        }
        catch (Exception ex) {
            var failureMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                Error = true,
                ErrorMessage = ex.Message
            };
            Sender.Tell(failureMsg);
        }
    }

    private WizZoneData LoadZoneData() {
        var data = _wad.OpenFile(ZONE_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load zone data from {zone}", 
                Logger.Args(ZONE_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<WizZoneData>(data?.ToArray(), 1, out var zoneData)) {
            Logger.Error("Failed to deserialize zone data from {zone}", 
                Logger.Args(ZONE_DATA_FILE_NAME));

            return null;
        }

        return zoneData;
    }

    private SpawnManager LoadSpawnData() {
        var data = _wad.OpenFile(SPAWN_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load spawn data from {zone}", 
                Logger.Args(SPAWN_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<SpawnManager>(data?.ToArray(), 1, out var spawnData)) {
            Logger.Error("Failed to deserialize spawn data from {zone}", 
                Logger.Args(SPAWN_DATA_FILE_NAME));

            return null;
        }

        return spawnData;
    }

    private PathTemplateList LoadPathData() {
        var data = _wad.OpenFile(PATH_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load path data from {zone}", 
                Logger.Args(PATH_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<PathTemplateList>(data?.ToArray(), 1, out var pathData)) {
            Logger.Error("Failed to deserialize path data from {zone}", 
                Logger.Args(PATH_DATA_FILE_NAME));

            return null;
        }

        return pathData;
    }

    private NodeTemplateList LoadNodeData() {
        var data = _wad.OpenFile(NODE_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load node data from {zone}", 
                Logger.Args(NODE_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<NodeTemplateList>(data?.ToArray(), 1, out var nodeData)) {
            Logger.Error("Failed to deserialize node data from {zone}", 
                Logger.Args(NODE_DATA_FILE_NAME));

            return null;
        }

        return nodeData;
    }

    private WizZoneVolumes LoadVolumeData() {
        var data = _wad.OpenFile(VOLUME_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load volume data from {zone}", 
                Logger.Args(VOLUME_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<WizZoneVolumes>(data?.ToArray(), 1, out var volumeData)) {
            Logger.Error("Failed to deserialize volume data from {zone}", 
                Logger.Args(VOLUME_DATA_FILE_NAME));

            return null;
        }

        return volumeData;
    }

    private WizZoneTriggers LoadTriggerData() {
        var data = _wad.OpenFile(TRIGGER_DATA_FILE_NAME);
        if (data is null) {
            Logger.Error("Failed to load trigger data from {zone}", 
                Logger.Args(TRIGGER_DATA_FILE_NAME));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<WizZoneTriggers>(data?.ToArray(), 1, out var triggerData)) {
            Logger.Error("Failed to deserialize trigger data from {zone}", 
                Logger.Args(TRIGGER_DATA_FILE_NAME));

            return null;
        }

        return triggerData;
    }

    private void LogLoadingStep(string step) {
        Logger.Debug("Loaded {step} in {Time}ms",
            Logger.Args(step, _benchmarkTimer.ElapsedMilliseconds));
        _benchmarkTimer.Restart();
    }

}