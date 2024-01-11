/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */
using Imlight.Common.Formats;

using Imlight.Common.ObjectProperty;

using Imlight.Common.ObjectProperty.PropertyReflection;

using System;

using System.IO;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Loads the Root.wad archive into memory.
/// </summary>
internal static class RootArchiveLoader {
    internal const string RootWadName = "Root.wad";
    private static KiWad s_rootWad;

    static RootArchiveLoader() => ReloadRootWad();

    internal static KiWad GetRootWad() => s_rootWad;

    /// <summary>
    /// Gets a <see cref="MemoryStream"/> for the specified file name.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>A <see cref="MemoryStream"/> containing the file data.</returns>
    internal static MemoryStream GetFileStream(string fileName) {
        var file = s_rootWad.OpenFile(fileName) ?? throw new Exception($"Could not find file {fileName} in Root.wad!");

        return file;
    }

    /// <summary>
    /// Retrieves a file of type T from the root archive.
    /// </summary>
    /// <typeparam name="T">The type of the file to retrieve.</typeparam>
    /// <param name="fileName">The name of the file to retrieve.</param>
    /// <returns>The file of type T.</returns>
    internal static T GetFile<T>(string fileName) where T : PropertyClass {
        // Validate that the file exists.
        var _ = s_rootWad.OpenFile(fileName) ?? throw new Exception($"Could not find file {fileName} in Root.wad!");

        var serializer = new FileSerializer();
        return serializer.OpenClass<T>(s_rootWad, fileName);
    }

    /// <summary>
    /// Reloads the Root.wad file into memory.
    /// </summary>
    internal static void ReloadRootWad() {
        if (!ResourceManager.TryLoadArchive(RootWadName, out s_rootWad)) {
            throw new Exception("Could not load Root.wad into memory!");
        }
    }
}
