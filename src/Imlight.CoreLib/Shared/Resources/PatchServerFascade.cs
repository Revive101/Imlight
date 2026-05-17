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
            Logger.Error("Could not get latest file list because the patch server is not available.");
            
            return default;
        }

        // Ask the patch server for the latest file list.
        var msg = new PATCH_105_PROTOCOL.MSG_LATESTFILELIST();
        var timeout = new TimeSpan(0, 0, s_pathServerWait);
        var patchServerAskTask = PatchServer.Instance.Ask<PATCH_105_PROTOCOL.MSG_LATESTFILELIST>(msg, timeout);

        var response = patchServerAskTask.Result;
        var latestFileList = response.LatestFileList;

        return latestFileList;
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
            Logger.Error("Could not get latest file list because the patch server is not available.");

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

            return true;
        }
        catch (Exception ex) {
            if (ex.Message.ToLower().Contains("task was cancelled")) {
                Logger.Warning("Download of wad {WadName} failed because the timeout of {Timer} was reached.",
                    Logger.Args(wadName, s_pathServerDownloadTimeout));

                return false;
            }

            // Unknown error occurred
            Logger.Error("Could not download wad {WadName}. Exception: {Ex}",
                Logger.Args(wadName, ex.Message));

            return false;
        }
    }

}
