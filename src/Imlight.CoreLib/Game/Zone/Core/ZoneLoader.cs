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
 * ZONE LOADING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Loads and deserializes game zone data from archive files, providing 
 * structured access to zone terrain, spawn points, paths, volumes, and triggers.
 * 
 * USAGE EXAMPLE:
 * var loaderActor = Context.ActorOf(Props.Create<ZoneLoader>());
 * loaderActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN { ZonePath = "MyWorld/Hub" });
 * 
 * NOTE:
 * The six files are read once per zone and kept in ZoneDataFileCache; each load deserializes
 * them again, in parallel, into objects of its own.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;
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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN))]
    private void ReceiveZoneBeginLoad(ZONE_102_PROTOCOL.MSG_ZONELOADBEGIN message) {
        var timer = Stopwatch.StartNew();
        var fromCache = ZoneDataFileCache.TryGet(message.ZonePath, out var files);
        if (!fromCache) {
            if (!ResourceManager.TryLoadArchive(message.ZonePath, out var wad)) {
                var failureMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                    ZonePath = message.ZonePath,
                    Error = true,
                    ErrorMessage = "Failed to load zone archive from local cache or patch server. "
                                 + "Likely, the patch server is unavailable."
                };
                Sender.Tell(failureMsg);

                return;
            }

            files = ReadZoneDataFiles(wad);
            ZoneDataFileCache.Add(message.ZonePath, files);
        }

        var readMs = timer.ElapsedMilliseconds;
        timer.Restart();

        // The files are independent, so they deserialize in parallel; gamedata.bin, usually the largest,
        // on this thread.
        var spawnTask = Task.Run(() => Deserialize<SpawnManager>(files.SpawnData, "spawn data", SPAWN_DATA_FILE_NAME));
        var pathTask = Task.Run(() => Deserialize<PathTemplateList>(files.PathData, "path data", PATH_DATA_FILE_NAME));
        var nodeTask = Task.Run(() => Deserialize<NodeTemplateList>(files.NodeData, "node data", NODE_DATA_FILE_NAME));
        var volumeTask = Task.Run(() => Deserialize<WizZoneVolumes>(files.VolumeData, "volume data", VOLUME_DATA_FILE_NAME));
        var triggerTask = Task.Run(() => Deserialize<WizZoneTriggers>(files.TriggerData, "trigger data", TRIGGER_DATA_FILE_NAME));
        Task<WizZoneData> zoneTask;
        try {
            zoneTask = Task.FromResult(Deserialize<WizZoneData>(files.ZoneData, "zone data", ZONE_DATA_FILE_NAME));
        } catch (Exception ex) {
            zoneTask = Task.FromException<WizZoneData>(ex);
        }

        try {
            Task.WaitAll(spawnTask, pathTask, nodeTask, volumeTask, triggerTask);
        } catch (AggregateException) {
            // Each step reports its own failure below, in load order.
        }

        // Wrap each loading step in a try-catch block to ensure that the actor
        // can still send a response to the client if an error occurs.
        try {
            var error = false;
            var errorMessage = string.Empty;

            var zoneData = TakeStep(zoneTask, "zone data", ref error, ref errorMessage);
            var spawnData = TakeStep(spawnTask, "spawn data", ref error, ref errorMessage);
            var pathData = TakeStep(pathTask, "path data", ref error, ref errorMessage);
            var nodeData = TakeStep(nodeTask, "node data", ref error, ref errorMessage);
            var volumeData = TakeStep(volumeTask, "volume data", ref error, ref errorMessage);
            var triggerData = TakeStep(triggerTask, "trigger data", ref error, ref errorMessage);

            Logger.Debug("Zone {ZonePath} files read in {ReadMs} ms ({Source}), deserialized in {DeserializeMs} ms.",
                Logger.Args(message.ZonePath, readMs, fromCache ? "cache" : "archive", timer.ElapsedMilliseconds));

            // Send completion message with all loaded data
            var completionMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                ZoneData = zoneData,
                SpawnData = spawnData,
                PathData = pathData,
                NodeData = nodeData,
                TriggerData = triggerData,
                VolumeData = volumeData,
                ZonePath = message.ZonePath,
                Error = error,
                ErrorMessage = error ? errorMessage : string.Empty
            };
            Sender.Tell(completionMsg);
        } catch (Exception ex) {
            var failureMsg = new ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS {
                Error = true,
                ErrorMessage = ex.Message,
                ZonePath = message.ZonePath
            };
            Sender.Tell(failureMsg);
        }
    }

    private static ZoneDataFiles ReadZoneDataFiles(Archive wad) => new(
        wad.OpenFile(ZONE_DATA_FILE_NAME)?.ToArray(),
        wad.OpenFile(SPAWN_DATA_FILE_NAME)?.ToArray(),
        wad.OpenFile(PATH_DATA_FILE_NAME)?.ToArray(),
        wad.OpenFile(NODE_DATA_FILE_NAME)?.ToArray(),
        wad.OpenFile(VOLUME_DATA_FILE_NAME)?.ToArray(),
        wad.OpenFile(TRIGGER_DATA_FILE_NAME)?.ToArray());

    private static T TakeStep<T>(Task<T> step, string stepName, ref bool error, ref string errorMessage)
        where T : PropertyClass {
        if (step.IsFaulted) {
            var ex = step.Exception.GetBaseException();

            throw new Exception($"Failed during {stepName} loading: {ex.Message}", ex);
        }

        var result = step.Result;
        if (result is null) {
            error = true;
            errorMessage = $"Failed to load {stepName}.";
        }

        return result;
    }

    private static T Deserialize<T>(byte[] data, string dataName, string fileName) where T : PropertyClass {
        if (data is null) {
            Logger.Error("Failed to load {DataName} from {FileName}", Logger.Args(dataName, fileName));

            return null;
        }

        var serializer = new BindSerializer();
        if (!serializer.Deserialize<T>(data, 1, out var result)) {
            Logger.Error("Failed to deserialize {DataName} from {FileName}", Logger.Args(dataName, fileName));

            return null;
        }

        return result;
    }

}
