/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Imlight.Common.Utilities;

namespace Imlight.Server.Shared.Resources
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
            var zoneList = new HashSet<string>();
            var doc = new XmlDocument();
            doc.Load(stream);
            
            foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone"))
            {
                var zoneName = zoneNode.InnerText;
                zoneList.Add(zoneName);
            }
            
            // Log
            Log.Information("AccessPassManager loaded {Count} zones.", Log.Args(zoneList.Count));

            _zones = zoneList.ToArray();

            return true;
        }
        
        public static bool DoesZoneExist(string zoneName)
        {
            return _zones.Any(zone => zone == zoneName);
        }
    }
}
