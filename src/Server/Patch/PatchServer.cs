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
using WizUnraveler;
using Imlight.Common.Utilities;
using Imlight.Common.Cryptography;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Patch
{
    public class PatchServer : Shared.Networking.Server
    {
        public const string DEFAULT_PATCH_SERVER_NAME = "Imlight.Patch";
        private const ushort DEFAULT_PATCH_SERVER_PORT = 12300;
        private const string PATCH_SERVER_URL = "http://phill030.de:12369/repatcher/";
        private const int PATCH_SERVER_TIMEOUT = 5; // In seconds.
        private const string LATEST_FILE_LIST_NAME_BIN = "LatestFileList.bin";
        private const string LATEST_FILE_LIST_NAME_XML = "LatestFileList.xml";
        private const int LATEST_FILE_LIST_PARSE_TIMEOUT = 5;
        private const uint REVISION = 735422;
        private const string USER_AGENT_VALUE = "KingsIsle Patcher";
        private const ushort DOWNLOAD_BUFFER_SIZE = 4096;

        public static IActorRef Instance { get; private set; }
        public static bool EndpointReached { get; private set; }
        public static uint LatestVersion { get; private set; }
        public static ByteString ListFileName { get; private set; }
        public static uint ListFileSize { get; private set; }
        public static uint ListFileCRC { get; private set; }
        public static ByteString ListFileURL { get; private set; }
        public static ByteString URLPrefix { get; private set; }
        public static ByteString URLSuffix { get; private set; }

        private LatestFileList _latestFileList;
        private Stopwatch _diagnosticStopwatch;

        public PatchServer(string name, int port, Props factoryProps) : base(name, port, factoryProps)
        {
            if (Instance is not null)
                throw new Exception("Attempted to create more than one patch server! This is not possible!");

            Log.Logger.Information($"Patch server created with " +
                                   $"name {name} " +
                                   $"under port {port}.");
            Instance = this.Self;
        }

        public static Props Props(
            string serverName = DEFAULT_PATCH_SERVER_NAME,
            ushort serverPort = DEFAULT_PATCH_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new PatchServer(serverName, serverPort, PatchServiceFactory.Props()));
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_INITIALIZE))]
        private void InitializeServer(SERVER_100_PROTOCOL.MSG_INITIALIZE message)
        {
            _diagnosticStopwatch = new Stopwatch();

            // Check patch server endpoint status and record the diagnostics.
            _diagnosticStopwatch.Restart();
            EndpointReached = GetPatchServerStatus();
            _diagnosticStopwatch.Stop();
            Log.Logger.Debug($"Patch server status check took {_diagnosticStopwatch.ElapsedMilliseconds} ms.");

            // Only perform the following if the patch server is available.
            if (!EndpointReached) return;

            // Download and parse the latest file list and record the diagnostics.
            _diagnosticStopwatch.Restart();
            //SetLatestFileList();
            _diagnosticStopwatch.Stop();
            Log.Logger.Debug($"Downloaded and parsed LatestFileList in {_diagnosticStopwatch.ElapsedMilliseconds} ms.");

            // Let whomever sender know that we're finished initializing!
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_INITIALIZE_COMPLETE());
        }

        [MessageHandler(typeof(PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_REQUEST))]
        public void ReceiveDownloadRequest(PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_REQUEST message)
        {
            var rsp = new PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_TASK();
            rsp.DownloadTask = DownloadFileStream(message.FileName);

            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES))]
        public void ReceiveLatestFileCacheProperties(PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES message)
        {
            var rsp = new PATCH_105_PROTCOL.MSG_LATEST_CACHE_PROPERTIES()
            {
                Name = ListFileName,
                URL = ListFileURL,
                URLPrefix = PATCH_SERVER_URL,
                URLSuffix = "",
                Version = LatestVersion,
                CRC = ListFileCRC,
                Size = ListFileSize,
            };

            Sender.Tell(rsp);
        }

        private async Task<byte[]> DownloadFileStream(string fileName)
        {
            if (!EndpointReached)
                throw new Exception("By this point, the patch server endpoint has not yet been reached!");

            var url = $"{PATCH_SERVER_URL}{fileName}";
            Log.Logger.Debug(url);

            try
            {
                // Create a new HttpClient with the magic user agent values.
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(USER_AGENT_VALUE);
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                //var progressBar = new ConsoleProgressBar();

                Log.Logger.Information($"Attempting to download file from patch server endpoint at url {url}. Content size: {totalBytes}");

                // Download the file from web using the HttpClient.
                await using Stream contentStream = await response.Content.ReadAsStreamAsync();
                MemoryStream memoryStream = new MemoryStream();

                var buffer = new byte[DOWNLOAD_BUFFER_SIZE];
                int bytesRead;
                long bytesDownloaded = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                    bytesDownloaded += bytesRead;

                    // Update the progress bar.
                    // TODO: I want a progress bar here, but it currently doesn't play nice with Serilog.
                    //var downloadedPercent = (double)(bytesDownloaded / totalBytes);
                    //progressBar.Report(downloadedPercent * 1);
                }

                Log.Logger.Information($"File successfully downloaded from {url}. Content size: {memoryStream.Length}");

                return memoryStream.ToArray();
            }
            catch (Exception webException)
            {
                Log.Logger.Error($"Error while downloading file from patch server endpoint: {webException.Message}");
                return null;
            }
        }

        private bool GetPatchServerStatus()
        {
            // Check to see if the patch server URL is available at all.
            Log.Logger.Information($"Checking patch server at URL {PATCH_SERVER_URL}. Timeout: {PATCH_SERVER_TIMEOUT} s.");
            if (!GetServerURLStatus(PATCH_SERVER_URL))
            {
                Log.Logger.Error($"Patch server at URL {PATCH_SERVER_URL} is not available.");
                return false;
            }

            Log.Logger.Information($"Patch server at URL {PATCH_SERVER_URL} found and set.");

            return true;
        }

        private bool GetServerURLStatus(string url)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(PATCH_SERVER_TIMEOUT);

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
                Log.Logger.Error($"Error while checking patch server at URL {url}. " +
                                 $"Exception: {ex.Message}");
                return false;
            }
        }

        private void SetLatestFileList()
        {
            // We need both versions of the LatestFileList (for now).
            // The first interpretation is xml, and is for the server to parse and cache.
            // We'll be using it to check the integrity of the server's files.
            var latestXml = DownloadFileStream(LATEST_FILE_LIST_NAME_XML).Result;
            if (latestXml is null)
            {
                Log.Logger.Error($"Had trouble downloading {LATEST_FILE_LIST_NAME_XML}.");
            }
            else
            {
                if (!ParseLatestFileList(latestXml, out var latestXmlObj))
                {
                    Log.Logger.Error($"Could not successfully parse {LATEST_FILE_LIST_NAME_XML}.");
                }
                else
                {
                    _latestFileList = latestXmlObj;
                }
            }

            // The second interpretation is the `.bin`, which is what the Wizard101 client uses.
            // Download the `.bin` interpreation and cache the file stats.
            // This is so we can easily return the statistics when the client requests it.
            var latestBin = DownloadFileStream(LATEST_FILE_LIST_NAME_BIN).Result;
            if (latestBin is null)
            {
                Log.Logger.Error($"Had trouble downloading the {LATEST_FILE_LIST_NAME_BIN}.");
            }
            else
            {
                // Cache the `.bin` file properties.
                LatestVersion = Convert.ToUInt32(REVISION);
                ListFileName = LATEST_FILE_LIST_NAME_BIN;
                ListFileSize = Convert.ToUInt32(latestBin.Length);
                ListFileCRC = crc32.Compute(latestBin);
                ListFileURL = $"{PATCH_SERVER_URL}{LATEST_FILE_LIST_NAME_BIN}";
            }
        }

        private bool ParseLatestFileList(byte[] content, out LatestFileList latestFileList)
        {
            latestFileList = null;

            // Convert the contents to an XmlDocument.
            var xml = StreamToXmlDoc(content);

            var rootNode = xml
                .GetElementsByTagName("LatestFileList")
                .Cast<XmlElement>()
                .FirstOrDefault();
            if (rootNode == null)
            {
                Log.Logger.Error("XmlDocument does not contain a LatestFileList node.");
                return false;
            }

            latestFileList = new LatestFileList() { Files = new List<LatestFile>() };
            foreach (var wadNode in rootNode.ChildNodes.Cast<XmlElement>())
            {
                if (wadNode.Name == "_TableList" || wadNode.Name == "About") continue;

                var internalRecord = wadNode.ChildNodes.Cast<XmlElement>().FirstOrDefault();
                if (internalRecord == null)
                {
                    Log.Logger.Error("WAD record does not contain a valid internal record.");
                    continue;
                }

                var wadRecord = new LatestFile()
                {
                    SourceFileName = internalRecord.SelectSingleNode("SrcFileName")?.InnerText,
                    TargetFileName = internalRecord.SelectSingleNode("TarFileName")?.InnerText,
                    FileType = TryParseUInt(internalRecord.SelectSingleNode("FileType")?.InnerText),
                    Size = TryParseUInt(internalRecord.SelectSingleNode("Size")?.InnerText),
                    HeaderSize = TryParseUInt(internalRecord.SelectSingleNode("HeaderSize")?.InnerText),
                    CompressedHeaderSize = TryParseUInt(internalRecord.SelectSingleNode("CompressedHeaderSize")?.InnerText),
                    Crc = TryParseUInt(internalRecord.SelectSingleNode("CRC")?.InnerText),
                    HeaderCrc = TryParseUInt(internalRecord.SelectSingleNode("HeaderCRC")?.InnerText),
                };

                latestFileList.Files.Add(wadRecord);
            }

            return true;
        }

        private XmlDocument StreamToXmlDoc(byte[] content)
        {
            // XmlDocument will not break on exception, for whatever god forsaken reason.
            // Fuck you, Microsoft.
            // This is our own catch to continue willingly even on exception.
            try 
            {
                var xmlDoc = new XmlDocument();
                var ms = new MemoryStream(content);
                xmlDoc.Load(ms);
                return xmlDoc;
            }
            catch (Exception ex) 
            {
                Log.Logger.Error($"Error parsing Stream to XmlDocument: {ex.Message}");
                return null;
            }

        }

        private uint TryParseUInt(string value)
        {
            uint.TryParse(value, out var result);
            return result;
        }
    }
}
