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

using Imcodec.ObjectProperty;
using Imcodec.Wad;
using Imlight.Common;
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

    /// <summary>
    /// Reads every file of the directory as a <typeparamref name="TTemplate"/>. A file that cannot be read is logged
    /// and skipped.
    /// </summary>
    /// <typeparam name="TTemplate">The template type the files hold.</typeparam>
    /// <param name="propertyMask">The property mask the files are read with.</param>
    /// <returns>Each file that was read, with its template, in directory order.</returns>
    protected List<(FileEntry File, TTemplate Template)> ReadTemplates<TTemplate>(
        PropertyFlags propertyMask = (PropertyFlags) BindSerializer.BiNDDefaultFlags) where TTemplate : PropertyClass {
        var templates = new List<(FileEntry File, TTemplate Template)>(Files.Count);
        foreach (var (file, data) in Files) {
            if (TryReadTemplate(file, data, propertyMask, out TTemplate template)) {
                templates.Add((file, template));
            }
        }

        return templates;
    }

    private static bool TryReadTemplate<TTemplate>(FileEntry file, Memory<byte>? data, PropertyFlags propertyMask,
                                                   out TTemplate template) where TTemplate : PropertyClass {
        template = null;
        if (data is null) {
            Logger.Error("Game data file {File} is empty and is skipped. Re-download the game resources.",
                Logger.Args(file.FileName));

            return false;
        }

        try {
            if (new BindSerializer().Deserialize(data.Value.ToArray(), propertyMask, out template) && template is not null) {
                return true;
            }

            Logger.Error("Game data file {File} could not be read and is skipped. Re-download the game resources.",
                Logger.Args(file.FileName));
        } catch (Exception ex) {
            Logger.Error("Game data file {File} could not be read and is skipped ({Reason}). Re-download the game resources.",
                Logger.Args(file.FileName, ex.Message));
        }

        template = null;

        return false;
    }

}
