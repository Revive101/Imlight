using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Imlight.Common;

namespace Imlight.Resources
{
    public static class AccessPassManager
    {
        private static string[] _zones;
        
        public static bool Load()
        {
            var workingZoneList = new List<string>();
            
            // Load the AccessPass.xml from resources.
            if (!ResourceManager.LoadFileStream("AccessPass.xml", out var fileStream))
            {
                Log.Logger.Fatal("AccessPassManager could not load AccessPass.xml.");
                return false;
            }
            
            // Use the XmlReader to read the file. Only the zone names are needed,
            // so we find the <Zone> tags and read the name attribute.
            var zoneCounter = 0;
            var doc = new XmlDocument();
            doc.Load(fileStream);
            
            foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone"))
            {
                var zoneName = zoneNode.InnerText;
                workingZoneList.Add(zoneName);
                zoneCounter++;
            }
            
            // Log
            Log.Logger.Information($"AccessPassManager loaded {zoneCounter} zones.");

            _zones = workingZoneList.ToArray();

            return true;
        }
        
        public static bool IsZone(string zoneName)
        {
            return _zones.Any(zone => zone == zoneName);
        }
    }
}