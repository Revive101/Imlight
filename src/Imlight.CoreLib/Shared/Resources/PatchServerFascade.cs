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
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// An interface for the patch server. Manages communication with the patch server,
/// and what happens when the patch server is not available.
/// </summary>
internal static class PatchServerFascade {

    private static readonly int s_pathServerWait = ConfigurationManager.Settings["ADvanced.LocalWadCacheWaitForPatchServerTimeout"].AsInt();
    private static readonly int s_pathServerDownloadTimeout = ConfigurationManager.Settings["Patch Server.PatchServerDownloadTimeout"].AsInt();
    internal static bool EndpointReached => PatchServer.EndpointReached;

    /// <summary>
    /// Gets the latest file list from the patch server.
    /// </summary>
    /// <returns>The latest file list.</returns>
    internal static LatestFileList GetLatestFileList() {
        if (!EndpointReached) {
            Logger.Warning("Could not get the game file list: the patch server is offline.");

            return default;
        }

        try {
            var msg = new PATCH_105_PROTOCOL.MSG_LATESTFILELIST();
            var timeout = new TimeSpan(0, 0, s_pathServerWait);

            return PatchServer.Instance.Ask<PATCH_105_PROTOCOL.MSG_LATESTFILELIST>(msg, timeout)
                .Result
                .LatestFileList;
        }
        catch (Exception ex) {
            Logger.Warning("Could not get the game file list from the patch server: {Reason}",
                Logger.Args(ex.GetBaseException().Message));

            return default;
        }
    }

    /// <summary>
    /// Downloads a WAD archive file from the patch server.
    /// </summary>
    /// <param name="wadName">The name of the WAD file to download.</param>
    /// <param name="fileStream">When this method returns, contains the downloaded WAD file as a <see cref="MemoryStream"/>. This parameter is passed uninitialized.</param>
    /// <returns><c>true</c> if the download was successful; otherwise, <c>false</c>.</returns>
    internal static bool DownloadWadFromPatchServer(string wadName, out MemoryStream fileStream) {
        fileStream = default;
        if (!EndpointReached) {
            Logger.Warning("Could not download game file {WadName}: the patch server is offline.",
                Logger.Args(wadName));

            return false;
        }

        try {
            var patchServer = PatchServer.Instance;
            wadName = wadName.Replace('/', '-');
            var askMsg = new PATCH_105_PROTOCOL.MSG_DOWNLOAD_WAD_REQUEST { WadName = wadName };
            var timeout = TimeSpan.FromSeconds(s_pathServerDownloadTimeout);
            fileStream = patchServer.Ask<PATCH_105_PROTOCOL.MSG_DOWNLOAD_FILE_RESULT>(askMsg, timeout)
                .Result
                .FileStream;

            return fileStream is not null;
        }
        catch (Exception ex) {
            if (ex.GetBaseException() is AskTimeoutException or TaskCanceledException) {
                Logger.Warning("Download of game file {WadName} gave up after {Seconds} s. Raise " +
                    "PatchServerDownloadTimeout in [Patch Server] if the patch server is slow.",
                    Logger.Args(wadName, s_pathServerDownloadTimeout));

                return false;
            }

            Logger.Error("Could not download game file {WadName}: {Reason}",
                Logger.Args(wadName, ex.GetBaseException().Message));

            return false;
        }
    }

}
