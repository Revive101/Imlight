/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common.Configuration;
using System;
using System.IO;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// An interface for the patch server. Manages communication with the patch server,
/// and what happens when the patch server is not available.
/// </summary>
internal static class PatchServerFascade {
    private static readonly int s_pathServerWait = ConfigurationManager.Settings.LocalWadCacheWaitForPatchServerTimeout;
    private static readonly int s_pathServerDownloadTimeout = ConfigurationManager.Settings.PatchServerDownloadTimeout;
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
    /// Downloads a WAD (Web Application Distribution) file from the patch server.
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
