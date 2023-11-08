/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Xml;

namespace DragonZoneTool.Managers;

public static class AccessPassManager
{
    private static readonly string AccessPassPath = Path.Combine(FileUtility.InputPath, "AccessPass.xml");
    private static readonly string DatabaseDefaultPath = Path.Combine(FileUtility.OutputPath, "serverdata");

    public static string[] GetAccessPassZones()
    {
        var stream = FileUtility.GetFileStream(AccessPassPath);
        if (stream is null) {
            throw new NullReferenceException($"AccessPass.xml was not found at path {AccessPassPath}.");
        }

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

        Console.WriteLine($"Loaded {zoneCounter} zones.");

        return zoneList.ToArray();
    }
}
