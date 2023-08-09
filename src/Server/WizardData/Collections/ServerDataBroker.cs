/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LiteDB;

namespace Imlight.Server.WizardData.Collections;

/// <summary>
///     Provides a static interface to the server's database.
/// </summary>
public static class ServerDataBroker
{
    // TODO: Make this configurable.
    private static readonly string _path = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
        "Content",
        $"serverdata");

    public static List<T> GetCollection<T>(string collectionName)
    {
        using var db = new LiteDatabase(_path);
        var table = db.GetCollection<T>(collectionName);
        
        return table.FindAll().ToList();
    }
}
