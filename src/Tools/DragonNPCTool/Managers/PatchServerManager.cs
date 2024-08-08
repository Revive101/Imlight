/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Formats;
using System.Net;

namespace DragonNPCTool.Managers;

public static class PatchServerManager {
    private const string PatchServerUrl = "https://patcher.phill030.de";
    private const string PatchServerWadUrlPrefix = "Data/GameData";
    private const int PatchServerTimeout = 10; // In seconds.
    private const uint GameClientRevision = 757029;
    private const string UserAgentValue = "KingsIsle Patcher";
    private const ushort DownloadBufferSize = 4096;
    private static string? _patchServerWorkingUrl;

    static PatchServerManager() {
        if (!GetPatchServerStatus()) {
            throw new Exception($"Patch server is not available!");
        }
    }

    public static bool IsPatchServerAvailable() => GetPatchServerStatus();

    public static KiWad DownloadWad(string wadName) {
        // Download the wad from the patch server.
        // Remove the `.wad` extension if one exists.
        if (wadName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) {
            wadName = wadName[..^4];
        }
        // Replace forward slashes with a hyphen.
        wadName = wadName.Replace('/', '-');

        var url = $"{_patchServerWorkingUrl}/{PatchServerWadUrlPrefix}/{wadName}.wad";
        var download = DownloadFileStream(url).Result;
        var newMs = new MemoryStream();
        download.Position = 0;
        download.CopyTo(newMs);
        newMs.Position = 0;
        return new KiWad(newMs);
    }

    private static bool GetPatchServerStatus() {
        var workingUrl = $"{PatchServerUrl}/V_r{GameClientRevision}.Wizard_1_550_0_Live";

        // Check to see if the patch server URL is available at all.
        Console.WriteLine($"Checking patch server at URL {workingUrl}. Timeout: {PatchServerTimeout} s.");
        if (!GetServerUrlStatus(workingUrl)) {
            Console.WriteLine($"Patch server at URL {workingUrl} is not available.");
            return false;
        }

        _patchServerWorkingUrl = workingUrl;
        Console.WriteLine($"Patch server at URL {workingUrl} found and set.");

        return true;
    }

    private static bool GetServerUrlStatus(string? url) {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
        client.Timeout = TimeSpan.FromSeconds(PatchServerTimeout);

        try {
            using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url)).Result;
            // Any response returned means the server is up.
            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError) {
            // Any response other than a 5xx error means the server is up.
            return ex.StatusCode < HttpStatusCode.InternalServerError;
        }
        catch (Exception ex) {
            Console.WriteLine($"Error while checking patch server at URL {url}. " +
                              $"Exception: {ex.Message}");
            return false;
        }
    }

    private static async Task<MemoryStream> DownloadFileStream(string url) {
        try {
            // Create a new HttpClient with the magic user agent values.
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            //var progressBar = new ConsoleProgressBar
            Console.WriteLine($"Attempting to download file from patch server endpoint at " +
                                   $"url {url}. " +
                                   $"Content size: {totalBytes}");
            // Download the file from web using the HttpClient.
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            var memoryStream = new MemoryStream();
            var buffer = new byte[DownloadBufferSize];
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                await memoryStream.WriteAsync(buffer, 0, bytesRead);
            }

            Console.WriteLine($"File successfully downloaded from {url}. Content size: {memoryStream.Length}");
            return memoryStream;
        }
        catch (Exception webException) {
            Console.WriteLine($"Error while downloading file from patch server endpoint: {webException.Message}");
            return null;
        }
    }

}
