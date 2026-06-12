/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using Imcodec.Wad;
using System;
using System.Collections.Generic;
using System.IO;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Represents a singleton class that provides access to resources located in the root directory.
/// </summary>
/// <typeparam name="T">The type of the derived class.</typeparam>
public abstract class RootDirectoryResourceSingleton<T> where T : RootDirectoryResourceSingleton<T>, new() {

    private static readonly Lazy<T> s_instance = new(() => new T());

    public static T Instance => s_instance.Value;

    protected abstract string DirectoryName { get; }
    protected Dictionary<FileEntry, Memory<byte>?> Files { get; private set; }

    protected RootDirectoryResourceSingleton() {
        if (!Load()) {
            throw new Exception($"Failed to load resource directory {DirectoryName}.");
        }

        AfterLoad();
    }

    /// <summary>
    /// Loads the files from the root directory.
    /// </summary>
    /// <returns>True if the files are successfully loaded; otherwise, false.</returns>
    protected virtual bool Load() {
        Files = RootArchiveLoader.GetDirectoryStream(DirectoryName);

        // Try to load this file from the root archive loader.
        return Files.Count > 0;
    }

    /// <summary>
    /// This method is called after the resource is loaded.
    /// </summary>
    protected abstract void AfterLoad();

}
