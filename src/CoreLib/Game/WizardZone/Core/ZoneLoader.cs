/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Akka.Event;
using Imlight.Common;
using Imlight.Common.Formats;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Core;

internal sealed class ZoneLoader : ReceiveProtocolDispatcher {

    private const string ZONE_DATA_FILE_NAME = "gamedata.bin";
    private const string SPAWN_DATA_FILE_NAME = "spawnData.xml";
    private const string PATH_DATA_FILE_NAME = "pathData.xml";
    private const string NODE_DATA_FILE_NAME = "pathNodeData.bin";
    private const string VOLUME_DATA_FILE_NAME = "volumes.xml";
    private const string TRIGGER_DATA_FILE_NAME = "triggers.xml";

    private KiWad _wad;
    private readonly FileSerializer _serializer = new();
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

    private WizZoneData LoadZoneData() 
        => _serializer.OpenClass<WizZoneData>(_wad, ZONE_DATA_FILE_NAME);

    private SpawnManager LoadSpawnData() 
        => _serializer.OpenClass<SpawnManager>(_wad, SPAWN_DATA_FILE_NAME);

    private PathManager_PathTemplateList LoadPathData() 
        => _serializer.OpenClass<PathManager_PathTemplateList>(_wad, PATH_DATA_FILE_NAME);

    private PathManager_NodeTemplateList LoadNodeData() 
        => _serializer.OpenClass<PathManager_NodeTemplateList>(_wad, NODE_DATA_FILE_NAME);

    private WizZoneVolumes LoadVolumeData() 
        => _serializer.OpenClass<WizZoneVolumes>(_wad, VOLUME_DATA_FILE_NAME);

    private WizZoneTriggers LoadTriggerData() 
        => _serializer.OpenClass<WizZoneTriggers>(_wad, TRIGGER_DATA_FILE_NAME);

    private void LogLoadingStep(string step) {
        Logger.Debug("Loaded {step} in {Time}ms", 
            Logger.Args(step, _benchmarkTimer.ElapsedMilliseconds));
        _benchmarkTimer.Restart();
    }

}