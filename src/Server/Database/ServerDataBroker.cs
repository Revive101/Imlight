/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.IO;
using System.Reflection;
using LiteDB;

namespace Imlight.Server.Database;

/// <summary>
///     Provides a static interface to the server's database.
/// </summary>
public static class ServerDataBroker
{
    // TODO: Make this configurable.
    private static readonly string _path = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? string.Empty, $"serverdata");

    public static ILiteCollection<T> GetCollection<T>()
    {
        using var db = new LiteDatabase(_path);
        return db.GetCollection<T>();
    }
}
