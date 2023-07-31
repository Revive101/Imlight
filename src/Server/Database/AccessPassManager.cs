/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Imlight.Common.Utilities;

namespace Imlight.Server.Database
{
    public static class AccessPassManager
    {
        const string ROOT_WAD_NAME = "Root.wad";
        const string ACCESS_PASS_NAME = "AccessPass.xml";
        
        private static string[] _zones;
        
        public static bool Load()
        {
            if (!ResourceManager.TryLoadFile(ROOT_WAD_NAME, ACCESS_PASS_NAME, out var stream))
                return false;

            // Use the XmlReader to read the file. Only the zone names are needed,
            // so we find the <Zone> tags and read the name attribute.
            var zoneList = new List<string>();
            var zoneCounter = 0;
            var doc = new XmlDocument();
            doc.Load(stream);
            
            foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone"))
            {
                var zoneName = zoneNode.InnerText;
                zoneList.Add(zoneName);
                zoneCounter++;
            }
            
            // Log
            Log.Logger.Information("AccessPassManager loaded {Count} zones.", zoneCounter);

            _zones = zoneList.ToArray();

            return true;
        }
        
        public static bool DoesZoneExist(string zoneName)
        {
            return _zones.Any(zone => zone == zoneName);
        }
    }
}
