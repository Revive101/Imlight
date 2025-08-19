/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Xml;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common;
using Imlight.CoreLib.Shared.Cryptography;
using Imcodec.IO;

namespace Imlight.CoreLib.Patch;

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
    // LatestFileList cached information.
    private static uint s_latestVersion;
    private static ByteString s_listFileName;
    private static uint s_listFileSize;
    private static uint s_listFileCrc;
    private static ByteString s_listFileUrl;
    private static ByteString s_urlPrefix;
    private static ByteString s_urlSuffix;

    private LatestFileList _latestFileList;
    private Stopwatch _diagnosticStopwatch;
    private string _patchServerWorkingUrl;

    public PatchServer(string name, int port, Props factoryProps) : base(name, port, factoryProps) {
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
    public void ReceiveDownloadRequest(PATCH_105_PROTOCOL.MSG_DOWNLOAD_WAD_REQUEST message) {
        var rsp = new PATCH_105_PROTOCOL.MSG_DOWNLOAD_FILE_RESULT {
            FileStream = DownloadWadStream(message.WadName).Result
        };

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES))]
    public void ReceiveLatestFileCacheProperties(PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES message) {
        var rsp = new PATCH_105_PROTOCOL.MSG_LATEST_CACHE_PROPERTIES() {
            Name = s_listFileName,
            URL = s_listFileUrl,
            URLPrefix = s_urlPrefix,
            URLSuffix = s_urlSuffix,
            Version = s_latestVersion,
            CRC = s_listFileCrc,
            Size = s_listFileSize,
        };

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(PATCH_105_PROTOCOL.MSG_LATESTFILELIST))]
    public void ReceiveLatestFileList(PATCH_105_PROTOCOL.MSG_LATESTFILELIST message) {
        var rsp = new PATCH_105_PROTOCOL.MSG_LATESTFILELIST() { LatestFileList = this._latestFileList };
        Sender.Tell(rsp);
    }

    private async Task<MemoryStream> DownloadWadStream(string wadName) {
        if (!EndpointReached) {
            throw new Exception("By this point, the patch server endpoint has not yet been reached!");
        }

        // Remove the `.wad` extension if one exists.
        if (wadName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) {
            wadName = wadName[..^4];
        }

        var url = $"{_patchServerWorkingUrl}/{PatchServerWadUrlPrefix}/{wadName}.wad";

        return await DownloadFileStream(url);
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

            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError) {
            return ex.StatusCode < HttpStatusCode.InternalServerError;
        }
        catch (Exception ex) {
            Logger.Error("Error while checking patch server at URL {Url}. Exception: {Ex}",
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

        // The second interpretation is the `.bin`, which is what the Wizard101 client uses.
        // Download the `.bin` interpretation and cache the file stats.
        var latestBin = DownloadStream(LatestFileListNameBin).Result;
        if (latestBin is null) {
            Logger.Error("Had trouble downloading the {Name}", Logger.Args(LatestFileListNameBin));

            return false;
        }

        // Process the actual revision from the revision string. It is right after the 'r' and before the '.'.
        var revisionStart = _revision.IndexOf('r') + 1;
        var revisionEnd = _revision.IndexOf('.');
        if (revisionStart == -1 || revisionEnd == -1) {
            Logger.Error("Could not parse the revision from the revision string.");

            return false;
        }
        var actualRevision = _revision[revisionStart..revisionEnd];

        // Cache the `.bin` file properties.
        s_latestVersion = Convert.ToUInt32(actualRevision);
        s_listFileName = LatestFileListNameBin;
        s_listFileUrl = $"{_patchServerWorkingUrl}/{LatestFileListNameBin}";
        s_listFileSize = Convert.ToUInt32(latestBin.Length);
        s_urlPrefix = _patchServerWorkingUrl;
        s_urlSuffix = "";

        // Convert the stream to a byte array to compute the crc32 hash.
        var ms = new MemoryStream();
        latestBin.Seek(0, SeekOrigin.Begin);
        latestBin.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        s_listFileCrc = Crc32.Calculate(uint.MaxValue, ms.ToArray()) ^ uint.MaxValue;

        return true;
    }

    private static bool ParseLatestFileList(Stream content, out LatestFileList latestFileList) {
        latestFileList = null;
        var xml = StreamToXmlDoc(content);

        var rootNode = xml
            .GetElementsByTagName("LatestFileList")
            .Cast<XmlElement>()
            .FirstOrDefault();
        if (rootNode == null) {
            Logger.Error("XmlDocument does not contain a LatestFileList node.");

            return false;
        }

        latestFileList = new LatestFileList { Files = new List<LatestFile>() };
        if (!ParseChildNodes(rootNode, latestFileList)) {
            return false;
        }

        var baseNode = xml
            .GetElementsByTagName("Base")
            .Cast<XmlElement>()
            .FirstOrDefault();
        if (baseNode != null) {
            return ParseChildNodes(baseNode, latestFileList);
        }

        Logger.Error("XmlDocument does not contain a Base node.");

        return false;
    }

    private static bool ParseChildNodes(XmlNode parentNode, LatestFileList latestFileList) {
        foreach (var latestFileNode in parentNode.ChildNodes.Cast<XmlElement>()) {
            if (latestFileNode.Name is "_TableList" or "About") {
                continue;
            }

            var isRecord = latestFileNode.Name == "RECORD";
            var def = ParseLatestFileXmlNode(latestFileNode, isRecord);
            latestFileList.Files.Add(def);
        }

        return true;
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
        // XmlDocument will not break on exception, for whatever god forsaken reason. Fuck you, Microsoft.
        // This is our own catch to continue willingly even on exception.
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
        uint.TryParse(value, out var result);

        return result;
    }

}
