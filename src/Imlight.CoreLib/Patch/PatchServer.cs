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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common;
using Imlight.CoreLib.Shared.Cryptography;

namespace Imlight.CoreLib.Patch;

/// <summary>
/// Akka.NET actor that manages communication with the remote patch server.
/// Downloads and caches the LatestFileList, serves file-list metadata to connected
/// clients, and downloads individual WAD archives on demand.
/// </summary>
/// <remarks>
/// Only one instance may exist (singleton enforced via <see cref="Instance"/>).
/// Initialization is triggered by <see cref="SERVER_100_PROTOCOL.MSG_INITIALIZE"/>
/// and blocks the caller until the remote endpoint is checked and the file list is cached.
/// </remarks>
public class PatchServer : Server {

    private const string PatchServerWadUrlPrefix = "Data/GameData";
    private const string LatestFileListNameBin = "LatestFileList.bin";
    private const string LatestFileListNameXml = "LatestFileList.xml";

    // Configuration values.
    private readonly string _userAgentValue
        = ConfigurationManager.Settings["Advanced.PatchServerUserAgent"];
    private readonly ushort _downloadBufferSize
        = ConfigurationManager.Settings["Advanced.PatchServerBufferSize"].AsUShort();
    private readonly string _revision
        = ConfigurationManager.Settings["Global Settings.GameRevision"];
    private readonly uint _patchServerTimeout
        = ConfigurationManager.Settings["Patch Server.PatchServerInternalTimeout"].AsUInt();
    private readonly string _patchServerInternalUrl
        = ConfigurationManager.Settings["Patch Server.PatchServerInternalUrl"];

    public static IActorRef Instance { get; private set; }
    public static bool EndpointReached { get; set; }

    private PatchCacheProperties _patchCacheProperties;
    private LatestFileList _latestFileList;
    private Stopwatch _diagnosticStopwatch;
    private string _patchServerWorkingUrl;

    // ctor
    public PatchServer(string name, int port, Props factoryProps)
        : base(name, port, factoryProps) {
        if (Instance is not null) {
            throw new Exception("Attempted to create more than one patch server! This is not possible!");
        }

        Logger.Information("Patch server created with name {name} under port {port}.", Logger.Args(name, port));
        Instance = this.Self;
    }

    public static Props Props(string serverName, ushort serverPort)
        => Akka.Actor.Props.Create(() => new PatchServer(serverName, serverPort, PatchServiceFactory.Props()));

    protected override void PreRestart(Exception reason, object message) {
        Logger.Error($"Patch server actor has restarted due to {reason.Message}");

        base.PreRestart(reason, message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_INITIALIZE))]
    private void InitializeServer(SERVER_100_PROTOCOL.MSG_INITIALIZE message) {
        // InitializeServer must be managed through the actor's mailbox. This is so we can return the Patch Server
        // status pseudo-synchronously as to block the main thread for following services that may depend on
        // the patch server. This function also has a stopwatch to diagnose any issues.
        _diagnosticStopwatch = new Stopwatch();

        // Check patch server endpoint status and record the diagnostics.
        _diagnosticStopwatch.Restart();
        EndpointReached = GetPatchServerStatus();
        _diagnosticStopwatch.Stop();
        Logger.Debug("Patch server status check took {em} ms.",
            Logger.Args(_diagnosticStopwatch.ElapsedMilliseconds));

        // Only perform the following if the patch server is available.
        if (!EndpointReached) {
            Logger.Error("Patch server endpoint is not available! Continuing without patch server.");

            Sender.Tell(new SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE());

            return;
        }

        // Download and parse the latest file list and record the diagnostics.
        _diagnosticStopwatch.Restart();
        var latestFileSuccess = SetLatestFileList();
        _diagnosticStopwatch.Stop();
        if (latestFileSuccess) {
            Logger.Debug("Downloaded and parsed LatestFileList in {em} ms.",
                Logger.Args(_diagnosticStopwatch.ElapsedMilliseconds));
        }

        // Let whomever sender know that we're finished initializing!
        Sender.Tell(new SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE());
    }

    [MessageHandler(typeof(PATCH_105_PROTOCOL.MSG_DOWNLOAD_WAD_REQUEST))]
    private void ReceiveDownloadRequest(PATCH_105_PROTOCOL.MSG_DOWNLOAD_WAD_REQUEST message) {
        var rsp = new PATCH_105_PROTOCOL.MSG_DOWNLOAD_FILE_RESULT {
            FileStream = DownloadWadStream(message.WadName).Result
        };

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(PATCH_105_PROTOCOL.MSG_LATESTFILELIST))]
    private void ReceiveLatestFileList(PATCH_105_PROTOCOL.MSG_LATESTFILELIST message) {
        var rsp = new PATCH_105_PROTOCOL.MSG_LATESTFILELIST() { LatestFileList = this._latestFileList };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES))]
    private void ReceiveLatestFileCacheProperties(PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES message) {
        var cache = _patchCacheProperties;
        var rsp = new PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES {
            Name = cache?.Name,
            URL = cache?.Url,
            URLPrefix = cache?.UrlPrefix,
            URLSuffix = cache?.UrlSuffix,
            Version = cache?.Version ?? 0,
            CRC = cache?.Crc ?? 0,
            Size = cache?.Size ?? 0,
            FileTime = cache?.FileTime ?? 0,
        };

        Sender.Tell(rsp);
    }

    private async Task<MemoryStream> DownloadWadStream(string wadName) {
        if (!EndpointReached) {
            throw new InvalidOperationException("Patch server endpoint is not available.");
        }

        // Remove the `.wad` extension if one exists.
        if (wadName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) {
            wadName = wadName[..^4];
        }

        var url = $"{_patchServerWorkingUrl}/{PatchServerWadUrlPrefix}/{wadName}.wad";

        var stream = await DownloadFileStream(url)
            ?? throw new InvalidOperationException($"Failed to download WAD '{wadName}' from patch server.");

        return stream;
    }

    private async Task<MemoryStream> DownloadStream(string fileName) {
        if (!EndpointReached) {
            throw new Exception("By this point, the patch server endpoint has not yet been reached!");
        }

        var url = $"{_patchServerWorkingUrl}/{fileName}";

        return await DownloadFileStream(url);
    }

    private async Task<MemoryStream> DownloadFileStream(string url) {
        try {
            // Create a new HttpClient with the magic user agent values.
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgentValue);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            var totalMegaBytes = totalBytes / 1024 / 1024;

            Logger.Information("Attempting to download file from patch server endpoint " +
                            "at url {Url}. Content size: {totalMegaBytes} MB", Logger.Args(url, totalMegaBytes));

            // Download the file from web using the HttpClient.
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            var memoryStream = new MemoryStream();

            var buffer = new byte[_downloadBufferSize];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0) {
                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }

            Logger.Information("File successfully downloaded from {Url}. Content size: {totalMegaBytes} MB",
                Logger.Args(url, totalMegaBytes));

            return memoryStream;
        }
        catch (Exception webException) {
            Logger.Error("Error while downloading file {File} from patch server endpoint: {Ex}",
                Logger.Args(url, webException.Message));

            return null;
        }
    }

    private bool GetPatchServerStatus() {
        var workingUrl = $"{_patchServerInternalUrl}/V_{_revision}";

        // Check to see if the patch server URL is available at all.
        Logger.Information("Checking patch server at URL {Url}. Timeout: {Timeout} s",
            Logger.Args(workingUrl, _patchServerTimeout));

        var serverStatus = GetServerUrlStatus(workingUrl).Result;
        if (!serverStatus) {
            Logger.Error("Patch server at URL {Url} is not available", Logger.Args(workingUrl));

            return false;
        }

        _patchServerWorkingUrl = workingUrl;
        Logger.Information("Patch server at URL {Url} found and set", Logger.Args(workingUrl));

        return true;
    }

    private async Task<bool> GetServerUrlStatus(string url) {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgentValue);
        client.Timeout = TimeSpan.FromSeconds(_patchServerTimeout);

        try {
            using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

            // Any response (2xx, 3xx, 4xx) means the server is reachable —
            // we only care that the endpoint exists, not that the path is valid.
            return true;
        }
        catch (HttpRequestException ex) {
            // Network-level failure (DNS, connection refused, TLS error) or
            // 5xx server error — the endpoint is not reachable.
            Logger.Warning("Patch server at URL {Url} is not reachable: {Reason}",
                Logger.Args(url, ex.Message));

            return false;
        }
    }

    private bool SetLatestFileList() {
        // We need both versions of the LatestFileList (for now).
        // The first interpretation is xml, and is for the server to parse and cache.
        // We'll be using it to check the integrity of Imlight's cached files.
        var latestXml = DownloadStream(LatestFileListNameXml).Result;
        if (latestXml is null) {
            Logger.Error("Had trouble downloading {Name}", Logger.Args(LatestFileListNameXml));

            return false;
        }

        latestXml.Seek(0, SeekOrigin.Begin);
        if (!ParseLatestFileList(latestXml, out var latestXmlObj)) {
            Logger.Error("Could not successfully parse {Name}", Logger.Args(LatestFileListNameXml));

            return false;
        }

        _latestFileList = latestXmlObj;

        // The second interpretation is the `.bin`, which is what the client uses.
        // Download the `.bin` interpretation and cache the file stats.
        var latestBin = DownloadStream(LatestFileListNameBin).Result;
        if (latestBin is null) {
            Logger.Error("Had trouble downloading the {Name}", Logger.Args(LatestFileListNameBin));

            return false;
        }

        // Compute CRC32 of the binary list.
        // Algorithm: CRC-32 with init=0, reflected polynomial 0xEDB88320, no final XOR.
        latestBin.Seek(0, SeekOrigin.Begin);
        uint crc;
        using (var ms = new MemoryStream()) {
            latestBin.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            crc = Crc32.Calculate(0, ms.ToArray());
        }

        // Cache the `.bin` file properties into the instance record.
        _patchCacheProperties = new PatchCacheProperties {
            Version = 1,
            Name = LatestFileListNameBin,
            Url = $"{_patchServerWorkingUrl}/{LatestFileListNameBin}",
            Size = Convert.ToUInt32(latestBin.Length),
            UrlPrefix = _patchServerWorkingUrl,
            UrlSuffix = "",
            Crc = crc,
            FileTime = (uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        return true;
    }

    private static bool ParseLatestFileList(Stream content, out LatestFileList latestFileList) {
        latestFileList = null;
        var xml = StreamToXmlDoc(content);
        if (xml is null) {
            return false;
        }

        var rootNode = xml
            .GetElementsByTagName("LatestFileList")
            .Cast<XmlElement>()
            .FirstOrDefault();
        if (rootNode == null) {
            Logger.Error("XmlDocument does not contain a LatestFileList node.");

            return false;
        }

        latestFileList = new LatestFileList { Files = new List<LatestFile>() };
        ParseChildNodes(rootNode, latestFileList);

        var baseNode = xml
            .GetElementsByTagName("Base")
            .Cast<XmlElement>()
            .FirstOrDefault();
        if (baseNode == null) {
            Logger.Error("XmlDocument does not contain a Base node.");

            return false;
        }

        ParseChildNodes(baseNode, latestFileList);

        return true;
    }

    private static void ParseChildNodes(XmlNode parentNode, LatestFileList latestFileList) {
        foreach (var latestFileNode in parentNode.ChildNodes.Cast<XmlElement>()) {
            if (latestFileNode.Name is "_TableList" or "About") {
                continue;
            }

            var isRecord = latestFileNode.Name == "RECORD";
            var def = ParseLatestFileXmlNode(latestFileNode, isRecord);
            if (def is not null) {
                latestFileList.Files.Add(def);
            }
        }
    }

    private static LatestFile ParseLatestFileXmlNode(XmlNode latestFileNode, bool isRecord = false) {
        // The needed data will be in a single nested node called RECORD.
        var internalRecord = latestFileNode;
        if (!isRecord) {
            internalRecord = latestFileNode.ChildNodes
                .Cast<XmlElement>()
                .FirstOrDefault();
        }

        if (internalRecord == null) {
            Logger.Error("LatestFile xml node did not contain a child RECORD.");

            return null;
        }

        // Create a LatestFile definition by parsing the given nodes.
        var wadRecord = new LatestFile() {
            SourceFileName = internalRecord.SelectSingleNode("SrcFileName")?.InnerText,
            TargetFileName = internalRecord.SelectSingleNode("TarFileName")?.InnerText,
            FileType = TryParseUInt(internalRecord.SelectSingleNode("FileType")?.InnerText),
            Size = TryParseUInt(internalRecord.SelectSingleNode("Size")?.InnerText),
            HeaderSize = TryParseUInt(internalRecord.SelectSingleNode("HeaderSize")?.InnerText),
            CompressedHeaderSize = TryParseUInt(internalRecord.SelectSingleNode("CompressedHeaderSize")?.InnerText),
            Crc = TryParseUInt(internalRecord.SelectSingleNode("CRC")?.InnerText),
            HeaderCrc = TryParseUInt(internalRecord.SelectSingleNode("HeaderCRC")?.InnerText),
        };

        return wadRecord;
    }

    private static XmlDocument StreamToXmlDoc(Stream content) {
        // XmlDocument.Load does not throw on all malformed XML — it can return
        // a partially-loaded document. We wrap in a try/catch to handle the cases
        // it does throw, and let callers validate the resulting document.
        try {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(content);

            return xmlDoc;
        }
        catch (Exception ex) {
            Logger.Error("Error parsing Stream to XmlDocument: {Ex}", Logger.Args(ex.Message));

            return null;
        }
    }

    private static uint TryParseUInt(string value) {
        _ = uint.TryParse(value, out var result);

        return result;
    }

}
