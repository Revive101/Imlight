using System;
using System.Reflection;
using System.Linq;
using System.IO;
using Imlight.Common.Cryptography;
using LiteDB;
using Imlight.Common.Utilities;
using WizUnraveler.Formats;

namespace Imlight.Server.Database
{
    public class FileDefinition
    {
        public string Filename { get; set; }
        public uint Crc { get; set; }
    }

    /// <summary>
    /// Abstractions for Imlight's local cache. It's safe it assume that the FileStorage of this cache will only
    /// ever contain KIWADs.
    /// </summary>
    public static class LocalCache
    {
        private static readonly string _path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? string.Empty, $"cache");

        /// <summary>
        /// Get or create a new collection in the cache database.
        /// </summary>
        /// <param name="collectionName"></param>
        public static ILiteCollection<T> GetCachedCollection<T>(string collectionName)
        {
            using var db = new LiteDatabase(_path);
            var col = db.GetCollection<T>(collectionName);

            return col;
        }

        /// <summary>
        /// Gets a KIWAD cached in the FileStorage.
        /// </summary>
        /// <param name="wadName"></param>
        /// <returns></returns>
        public static Wad GetCachedWad(string wadName)
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();

            var file = fs.Find(f => f.Filename == wadName)
                .FirstOrDefault();

            if (file is null)
                return null;
            
            // LiteDB's stream is pretty bare and doesn't offer a lot of methods. Convert the file to a MemoryStream
            // so we have more control over it.
            var ms = new MemoryStream();
            file.OpenRead().CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            return new Wad(ms);
        }

        /// <summary>
        /// Caches a file into the local database.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="contentStream"></param>
        public static void CacheWad(string fileName, Stream contentStream)
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();

            // Search to see if this file exists. If it does, we'll warn the user
            // but we will overwrite the file regardless.
            var file = fs.Find(f => f.Filename == fileName)
                .FirstOrDefault();

            if (file is not null)
            {
                Log.Logger.Warning($"LocalCache already contains a file definition for \"{fileName}\"! " +
                                   $"File will be overwritten.");
            }
            
            // Create a new MemoryStream of the content stream so we don't consume the parameter.
            var ms = new MemoryStream();
            contentStream.Position = 0;
            contentStream.CopyTo(ms);
            contentStream.Position = 0;
            ms.Position = 0;

            // Create a new FileDefinition. Create a new byte array from the content stream.
            var def = new FileDefinition
            {
                Filename = fileName,
                Crc = crc32.Compute(ms.ToArray())
            };
            
            fs.Upload(def, fileName, ms);
        }
    }
}
