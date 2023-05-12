using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Xml;
using Akka.Actor;
using Imlight.Common.Utilities;

namespace Imlight.Server.Patch
{
    public class PatchServer : Shared.Networking.Server
    {
        public static IActorRef Instance { get; private set; }
        
        public const string DEFAULT_PATCH_SERVER_NAME = "Imlight.Patch";
        private const ushort DEFAULT_PATCH_SERVER_PORT = 12300;
        private const string PATCH_SERVER_URL = "http://versionec.us.wizard101.com/WizPatcher/";
        private const string REVISION = "735422";
        private const int PATCH_SERVER_TIMEOUT = 15000;

        private bool _patchEndpointAlive;
        private string _patchServerWorkingUrl;
        private LatestFileList _latestFileList;
        private readonly Stopwatch _diagnosticStopwatch;
        
        public PatchServer(string name, int port, Props factoryProps) : base(name, port, factoryProps)
        {
            Log.Logger.Information($"Patch server created with " +
                                   $"name {name} " +
                                   $"under port {port}.");
            Instance = this.Self;

            // Check patch server status and record the diagnostics.
            _diagnosticStopwatch = new Stopwatch();
            _diagnosticStopwatch.Restart();
            _patchEndpointAlive = GetPatchServerStatus();
            _diagnosticStopwatch.Stop();
            Log.Logger.Debug($"Patch server status check took {_diagnosticStopwatch.ElapsedMilliseconds} ms.");

            // Only perform the following if the patch server is available.
            if (!_patchEndpointAlive) return;

            // Set the latest file list and record the diagnostics.
            _diagnosticStopwatch.Restart();
            if (!TryGetLatestFileList(out var _latestFileList)) 
            {
                Log.Logger.Error("Trouble getting LatestFileList.xml from the patch server!");
            }
            _diagnosticStopwatch.Stop();
            Log.Logger.Debug($"Parsed LatestFileList.xml in {_diagnosticStopwatch.ElapsedMilliseconds} ms.");
        }
        
        public static Props Props(
            string serverName = DEFAULT_PATCH_SERVER_NAME,
            ushort serverPort = DEFAULT_PATCH_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new PatchServer(serverName, serverPort, null));
        }

        private bool GetPatchServerStatus()
        {
            var patchServerUrl = PATCH_SERVER_URL;
            var revisionUrl = $"{patchServerUrl}V_r{REVISION}.Wizard_1_510_Live/Windows/";

            Log.Logger.Information($"Checking patch server at URL {patchServerUrl}. Timeout: {PATCH_SERVER_TIMEOUT} ms.");
            if (!GetPatchServerStatus(patchServerUrl))
            {
                Log.Logger.Error($"Patch server at URL {patchServerUrl} is not available.");
                return false;
            }

            Log.Logger.Information($"Checking patch server revision at URL {revisionUrl}. Timeout: {PATCH_SERVER_TIMEOUT} ms.");
            if (!GetPatchServerDirectoryStatus(revisionUrl))
            {
                Log.Logger.Error($"Patch server is available, but revision {REVISION} is not available.");
                return false;
            }
        
            return true;
        }

        private bool GetPatchServerStatus(string url)
        {
            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Timeout = PATCH_SERVER_TIMEOUT;
            request.Method = "HEAD";
            try
            {
                using var response = (HttpWebResponse) request.GetResponse();
                // Any response returned means the server is up.
                return true;
            }
            catch (WebException ex)
            {
                // Check if a response was received.
                if (ex.Response != null)
                {
                    // Any response other than a 5xx error means the server is up.
                    var response = (HttpWebResponse)ex.Response;
                    return (int)response.StatusCode < 500;
                }
                else
                {
                    Log.Logger.Error($"Error while checking patch server at URL {url}. " +
                                     $"Exception: {ex.Message}");
                    return false;
                }
            }
        }

        private bool GetPatchServerDirectoryStatus(string url)
        {
            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Timeout = PATCH_SERVER_TIMEOUT;
            request.Method = "HEAD";

            try
            {
                using var response = (HttpWebResponse) request.GetResponse();
                return true;
            }
            catch (WebException ex)
            {
                // Return true if we get a 403. Forbidden means the directory exists.
                if (ex.Response != null)
                {
                    var response = (HttpWebResponse)ex.Response;
                    return response.StatusCode == HttpStatusCode.Forbidden;
                }
                
                Log.Logger.Error($"Error while checking patch server at URL {url}. " +
                                 $"Exception: {ex.Message}");
                return false;
            }
        }

        private bool TryGetLatestFileList(out LatestFileList latestFileList)
        {
            latestFileList = default;

            if (!DownloadLatestFileList(out var xmlDocument)
             || !ParseLatestFileList(xmlDocument, out latestFileList))
            {
                return false;
            }
            
            return true;
        }

        private bool DownloadLatestFileList(out XmlDocument xmlDocument)
        {
            xmlDocument = null;
            var url = $"{_patchServerWorkingUrl}LatestFileList.bin";
            Log.Logger.Information($"Downloading latest file list from patch server at URL {url}.");
            
            // Download the file list.
            try
            {
                using var client = new HttpClient();
                using var response = client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
            
                using var content = response.Content.ReadAsStreamAsync().Result;

                // Convert the contents to an XmlDocument.
                xmlDocument = new XmlDocument();
                using var reader = new System.IO.StreamReader(content);
                var xmlContent = reader.ReadToEnd();
                xmlDocument.LoadXml(xmlContent);

                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Error while downloading latest file list from patch server at URL {url}. " +
                                 $"Exception: {ex.Message}");

                return false;
            }
        }
        
        private bool ParseLatestFileList(XmlDocument xmlDocument, out LatestFileList latestFileList)
        {
            latestFileList = null;
            var rootNode = xmlDocument.GetElementsByTagName("LatestFileList").Cast<XmlElement>().FirstOrDefault();
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

        private uint TryParseUInt(string value)
        {
            uint.TryParse(value, out var result);
            return result;
        }
    }
}
