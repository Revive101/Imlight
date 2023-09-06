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
using Imlight.Common.Configuration;
using WizUnraveler;
using Imlight.Common.Utilities;
using Imlight.Common.Cryptography;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.IO;

namespace Imlight.Server.Patch
{
    public class PatchServer : Shared.Networking.Server
    {
        private const string PatchServerWadUrlPrefix = "wads";
        private const string PatchServerUtilUrlPrefix = "utils";
        private const string LatestFileListNameBin = "LatestFileList.bin";
        private const string LatestFileListNameXml = "LatestFileList.xml";
        // Configuration values.
        private readonly string UserAgentValue = ConfigurationManager.Settings.PatchServerUserAgent;
        private readonly ushort DownloadBufferSize = ConfigurationManager.Settings.PatchServerBufferSize;
        private readonly uint Revision = ConfigurationManager.Settings.GameRevision;
        private readonly uint PatchServerTimeout = ConfigurationManager.Settings.PatchServerInternalTimeout;
        private readonly string PatchServerInternalUrl = ConfigurationManager.Settings.PatchServerInternalUrl;
        private readonly ushort PatchServerInternalPort = ConfigurationManager.Settings.PatchServerInternalPort;
        // "http://phill030.de:12369/patcher/";

        public static IActorRef Instance { get; private set; }
        public static bool EndpointReached { get; set; }
        // LatestFileList cached information.
        private static uint _latestVersion;
        private static ByteString _listFileName;
        private static uint _listFileSize;
        private static uint _listFileCrc;
        private static ByteString _listFileUrl;
        private static ByteString _urlPrefix;
        private static ByteString _urlSuffix;

        private LatestFileList _latestFileList;
        private Stopwatch _diagnosticStopwatch;
        private string _patchServerWorkingUrl;

        public PatchServer(string name, int port, Props factoryProps) : base(name, port, factoryProps)
        {
            if (Instance is not null)
                throw new Exception("Attempted to create more than one patch server! This is not possible!");

            Log.Information("Patch server created with name {name} under port {port}.", Log.Args(name, port));
            Instance = this.Self;
        }

        public static Props Props(string serverName, ushort serverPort)
        {
            return Akka.Actor.Props.Create(() => new PatchServer(serverName, serverPort, PatchServiceFactory.Props()));
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Log.Error($"Patch server actor has restarted due to {reason.Message}");
            
            base.PreRestart(reason, message);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_INITIALIZE))]
        private void InitializeServer(SERVER_100_PROTOCOL.MSG_INITIALIZE message)
        {
            // InitializeServer must be managed through the actor's mailbox. This is so we can return the Patch Server
            // status pseudo-synchronously as to block the main thread for following services that may depend on
            // the patch server. This function also has a stopwatch to diagnose any issues.
            _diagnosticStopwatch = new Stopwatch();

            // Check patch server endpoint status and record the diagnostics.
            _diagnosticStopwatch.Restart();
            EndpointReached = GetPatchServerStatus();
            _diagnosticStopwatch.Stop();
            Log.Debug("Patch server status check took {em} ms.", 
                Log.Args(_diagnosticStopwatch.ElapsedMilliseconds));

            // Only perform the following if the patch server is available.
            if (!EndpointReached)
            {
                Log.Error("Patch server endpoint is not available! Continuing without patch server.");
                
                Sender.Tell(new SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE());
                return;
            }

            // Download and parse the latest file list and record the diagnostics.
            _diagnosticStopwatch.Restart();
            var latestFileSuccess = SetLatestFileList();
            _diagnosticStopwatch.Stop();
            if (latestFileSuccess)
                Log.Debug("Downloaded and parsed LatestFileList in {em} ms.", 
                    Log.Args(_diagnosticStopwatch.ElapsedMilliseconds));

            // Let whomever sender know that we're finished initializing!
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE());
        }

        [MessageHandler(typeof(PATCH_105_PROTCOL.MSG_DOWNLOAD_WAD_REQUEST))]
        public void ReceiveDownloadRequest(PATCH_105_PROTCOL.MSG_DOWNLOAD_WAD_REQUEST message)
        {
            var rsp = new PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_RESULT
            {
                FileStream = DownloadWadStream(message.WadName).Result
            };

            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES))]
        public void ReceiveLatestFileCacheProperties(PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES message)
        {
            var rsp = new PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES()
            {
                Name = _listFileName,
                URL = _listFileUrl,
                URLPrefix = _urlPrefix,
                URLSuffix = _urlSuffix,
                Version = _latestVersion,
                CRC = _listFileCrc,
                Size = _listFileSize,
            };

            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(PATCH_105_PROTCOL.MSG_LATESTFILELIST))]
        public void ReceiveLatestFileList(PATCH_105_PROTCOL.MSG_LATESTFILELIST message)
        {
            var rsp = new PATCH_105_PROTCOL.MSG_LATESTFILELIST() { LatestFileList = this._latestFileList };
            Sender.Tell(rsp);
        }

        private async Task<MemoryStream> DownloadWadStream(string wadName)
        {
            if (!EndpointReached)
                throw new Exception("By this point, the patch server endpoint has not yet been reached!");
            
            // Remove the `.wad` extension if one exists.
            if (wadName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
                wadName = wadName[..^4];

            var url = $"{_patchServerWorkingUrl}/{PatchServerWadUrlPrefix}/{wadName}.wad";

            return await DownloadFileStream(url);
        }
        
        private async Task<MemoryStream> DownloadUtilityStream(string fileName)
        {
            if (!EndpointReached)
                throw new Exception("By this point, the patch server endpoint has not yet been reached!");
            
            var url = $"{_patchServerWorkingUrl}/{PatchServerUtilUrlPrefix}/{fileName}";

            return await DownloadFileStream(url);
        }

        private async Task<MemoryStream> DownloadLatestFileList()
        {
            if (!EndpointReached)
                throw new Exception("By this point, the patch server endpoint has not yet been reached!");
            
            var url = $"{_patchServerWorkingUrl}";

            return await DownloadFileStream(url);
        }

        private async Task<MemoryStream> DownloadFileStream(string url)
        {
            try
            {
                // Create a new HttpClient with the magic user agent values.
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;

                Log.Information("Attempting to download file from patch server endpoint " +
                                       "at url {Url}. Content size: {TotalBytes}", Log.Args(url, totalBytes));

                // Download the file from web using the HttpClient.
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                var memoryStream = new MemoryStream();

                var buffer = new byte[DownloadBufferSize];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                }

                Log.Information("File successfully downloaded from {Url}. Content size: {Size}", 
                    Log.Args(url, memoryStream.Length));

                return memoryStream;
            }
            catch (Exception webException)
            {
                Log.Error("Error while downloading file {File} from patch server endpoint: {Ex}",
                    Log.Args(url, webException.Message));
                return null;
            }
        }

        private bool GetPatchServerStatus()
        {
            var internalUrl = $"{PatchServerInternalUrl}:{PatchServerInternalPort}/patcher";
            var workingUrl = $"{internalUrl}/V_r{Revision}.Wizard_1_520";

            // Check to see if the patch server URL is available at all.
            Log.Information("Checking patch server at URL {Url}. Timeout: {Timeout} s", 
                 Log.Args( workingUrl, PatchServerTimeout));
            if (!GetServerUrlStatus(workingUrl))
            {
                Log.Error("Patch server at URL {Url} is not available", Log.Args(workingUrl));
                return false;
            }

            _patchServerWorkingUrl = workingUrl;
            Log.Information("Patch server at URL {Url} found and set", Log.Args(workingUrl));

            return true;
        }

        private bool GetServerUrlStatus(string url)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentValue);
            client.Timeout = TimeSpan.FromSeconds(PatchServerTimeout);

            try
            {
                using var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url)).Result;
                // Any response returned means the server is up.
                return true;
            }
            catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError)
            {
                // Any response other than a 5xx error means the server is up.
                return ex.StatusCode < HttpStatusCode.InternalServerError;
            }
            catch (Exception ex)
            {
                Log.Error("Error while checking patch server at URL {Url}. Exception: {Ex}", 
                    Log.Args(url, ex.Message));
                return false;
            }
        }

        private bool SetLatestFileList()
        {
            // We need both versions of the LatestFileList (for now).
            // The first interpretation is xml, and is for the server to parse and cache.
            // We'll be using it to check the integrity of Imlight's cached files.
            var latestXml = DownloadLatestFileList().Result;
            if (latestXml is null)
            {
                Log.Error("Had trouble downloading {Name}", Log.Args(LatestFileListNameXml));
                return false;
            }

            latestXml.Seek(0, SeekOrigin.Begin);
            if (!ParseLatestFileList(latestXml, out var latestXmlObj))
            {
                Log.Error("Could not successfully parse {Name}", Log.Args(LatestFileListNameXml));
                return false;
            }

            _latestFileList = latestXmlObj;

            // The second interpretation is the `.bin`, which is what the Wizard101 client uses.
            // Download the `.bin` interpretation and cache the file stats.
            var latestBin = DownloadUtilityStream(LatestFileListNameBin).Result;
            if (latestBin is null)
            {
                Log.Error("Had trouble downloading the {Name}", Log.Args(LatestFileListNameBin));
                return false;
            }

            // Cache the `.bin` file properties.
            _latestVersion = Convert.ToUInt32(Revision);
            _listFileName = LatestFileListNameBin;
            _listFileUrl = $"{_patchServerWorkingUrl}/{LatestFileListNameBin}";
            _listFileSize = Convert.ToUInt32(latestBin.Length);
            _urlPrefix = _patchServerWorkingUrl;
            _urlSuffix = "";

            // Convert the stream to a byte array to compute the crc32 hash.
            var ms = new MemoryStream();
            latestBin.Seek(0, SeekOrigin.Begin);
            latestBin.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            _listFileCrc = crc32.Compute(ms.ToArray());

            return true;
        }

        private static bool ParseLatestFileList(Stream content, out LatestFileList latestFileList)
        {
            latestFileList = null;
            var xml = StreamToXmlDoc(content);

            var rootNode = xml
                .GetElementsByTagName("LatestFileList")
                .Cast<XmlElement>()
                .FirstOrDefault();
            if (rootNode == null)
            {
                Log.Error("XmlDocument does not contain a LatestFileList node.");
                return false;
            }

            latestFileList = new LatestFileList { Files = new List<LatestFile>() };
            if (!ParseChildNodes(rootNode, latestFileList))
                return false;

            var baseNode = xml
                .GetElementsByTagName("Base")
                .Cast<XmlElement>()
                .FirstOrDefault();
            if (baseNode != null) 
                return ParseChildNodes(baseNode, latestFileList);
            
            Log.Error("XmlDocument does not contain a Base node.");
            return false;
        }

        private static bool ParseChildNodes(XmlNode parentNode, LatestFileList latestFileList)
        {
            foreach (var latestFileNode in parentNode.ChildNodes.Cast<XmlElement>())
            {
                if (latestFileNode.Name is "_TableList" or "About")
                    continue;

                var isRecord = latestFileNode.Name == "RECORD";
                var def = ParseLatestFileXmlNode(latestFileNode, isRecord);
                latestFileList.Files.Add(def);
            }

            return true;
        }

        private static LatestFile ParseLatestFileXmlNode(XmlNode latestFileNode, bool isRecord = false)
        {
            // The needed data will be in a single nested node called RECORD.
            var internalRecord = latestFileNode;
            if (!isRecord)
                internalRecord = latestFileNode.ChildNodes
                    .Cast<XmlElement>()
                    .FirstOrDefault();
            if (internalRecord == null)
            {
                Log.Error("LatestFile xml node did not contain a child RECORD.");
                return null;
            }
            
            // Create a LatestFile definition by parsing the given nodes.
            var wadRecord = new LatestFile()
            {
                SourceFileName       = internalRecord.SelectSingleNode("SrcFileName")?.InnerText,
                TargetFileName       = internalRecord.SelectSingleNode("TarFileName")?.InnerText,
                FileType             = TryParseUInt(internalRecord.SelectSingleNode("FileType")?.InnerText),
                Size                 = TryParseUInt(internalRecord.SelectSingleNode("Size")?.InnerText),
                HeaderSize           = TryParseUInt(internalRecord.SelectSingleNode("HeaderSize")?.InnerText),
                CompressedHeaderSize = TryParseUInt(internalRecord.SelectSingleNode("CompressedHeaderSize")?.InnerText),
                Crc                  = TryParseUInt(internalRecord.SelectSingleNode("CRC")?.InnerText),
                HeaderCrc            = TryParseUInt(internalRecord.SelectSingleNode("HeaderCRC")?.InnerText),
            };

            return wadRecord;
        }

        private static XmlDocument StreamToXmlDoc(Stream content)
        {
            // XmlDocument will not break on exception, for whatever god forsaken reason. Fuck you, Microsoft.
            // This is our own catch to continue willingly even on exception.
            try 
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(content);
                return xmlDoc;
            }
            catch (Exception ex) 
            {
                Log.Error("Error parsing Stream to XmlDocument: {Ex}", Log.Args(ex.Message));
                return null;
            }
        }

        private static uint TryParseUInt(string value)
        {
            uint.TryParse(value, out var result);
            return result;
        }
    }
}
