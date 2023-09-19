/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Reflection;

namespace DragonZoneTool;

public static class FileUtility
{
    public static readonly string InputPath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "input");
    public static readonly string OutputPath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "output");
    
    public static MemoryStream? GetFileStream(string path)
    {
        if (!File.Exists(path))
            return null;

        var fs = File.ReadAllBytes(path);
        var ms = new MemoryStream(fs);
        ms.Position = 0;

        return ms;
    }
}