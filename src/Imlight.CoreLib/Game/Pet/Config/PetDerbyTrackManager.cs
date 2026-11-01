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
 * PET DERBY TRACKS
 * ========================================================================
 *
 * PURPOSE:
 * Loads the pet derby tuning values and the track list from the root archive.
 *
 * USAGE EXAMPLE:
 * var track = PetDerbyTrackManager.GetRandomTrack(rank);
 * var morale = PetDerbyTrackManager.Tuning.m_moraleBase;
 *
 * NOTE:
 * The tuning object is the file's own data, so every race constant the client uses is read from it.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using System;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The pet derby race tuning and the list of derby tracks, as defined in PetDerbyTracks.xml.
/// </summary>
internal sealed class PetDerbyTrackManager : RootSingleResourceSingleton<PetDerbyTrackManager>, IMemoryStreamDisposable {

    protected override string ResourceName => "PetDerbyTracks.xml";

    private static Imcodec.ObjectProperty.TypeCache.PetDerbyTrackManager s_manager;
    private static Dictionary<ulong, PetDerbyTrackInfo> s_tracksById = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<Imcodec.ObjectProperty.TypeCache.PetDerbyTrackManager>(Stream.ToArray(), out var manager)) {
            Logger.Error("Failed to deserialize PetDerbyTracks.xml");

            return;
        }

        var byId = new Dictionary<ulong, PetDerbyTrackInfo>();
        foreach (var track in manager.m_tracks) {
            if (!byId.TryAdd(track.m_trackID.Full, track)) {
                Logger.Error("PetDerbyTracks.xml lists the track {TrackId} twice.", Logger.Args(track.m_trackID.Full));
            }
        }

        s_manager = manager;
        s_tracksById = byId;
        Logger.Information("Loaded {Count} pet derby tracks.", Logger.Args(manager.m_tracks.Count));

        DisposeStream();
    }

    /// <summary>
    /// The race tuning values, terrain effects and cheer and slow effects of the file.
    /// </summary>
    internal static Imcodec.ObjectProperty.TypeCache.PetDerbyTrackManager Tuning => s_manager;

    /// <summary>
    /// Every derby track in file order.
    /// </summary>
    internal static IReadOnlyList<PetDerbyTrackInfo> Tracks => s_manager?.m_tracks ?? [];

    /// <summary>
    /// Finds a track by its id.
    /// </summary>
    /// <param name="trackId">The track id.</param>
    /// <param name="track">The track.</param>
    /// <returns>False if no track has that id.</returns>
    internal static bool TryGetTrack(GID trackId, out PetDerbyTrackInfo track)
        => s_tracksById.TryGetValue(trackId.Full, out track);

    /// <summary>
    /// Picks one of the tracks of a rank at random, the way the client does for a practice race.
    /// </summary>
    /// <param name="rank">The track rank.</param>
    /// <returns>A track of that rank, or null if none has it.</returns>
    internal static PetDerbyTrackInfo GetRandomTrack(byte rank) {
        var matching = Tracks.Where(track => track.m_rank == rank).ToList();

        return matching.Count == 0 ? null : matching[Random.Shared.Next(matching.Count)];
    }

    public void DisposeStream()
        => base.Stream.Dispose();

}
