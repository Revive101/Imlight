using System;
using System.Reflection;
using System.Linq;
using System.IO;
using LiteDB;
using Imlight.Common.Utilities;
using Imlight.Common.Cryptography;

namespace Imlight.Server.Database
{
    public class FileDefinition
    {
        public string Filename { get; set; }
        public uint crc { get; set; }
    }

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

        public static bool TryGetCachedFile(string fileName, out byte[] fileRaw)
        {
            fileRaw = default;
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();

            var file = fs.Find(f => f.Filename == fileName)
                .FirstOrDefault();

            if (file is null)
                return false;

            var memStream = new MemoryStream();
            file.CopyTo(memStream);
            fileRaw = memStream.ToArray();

            return true;
        }

        public static void CacheFile(string fileName, Stream contentStream)
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();

            // Search to see if this file exists. If it does, we'll warn the user
            // but we will overwrite the file regardless.
            var file = fs.Find(f => f.Filename == fileName)
                .FirstOrDefault();

            if (file is not null)
            {
                Log.Logger.Warning($"LocalCache already contains a file definition for \"{fileName}\"! File will be overwritten.");
            }

            // Create a new FileDefinition. Create a new byte array from the content stream.
            var def = new FileDefinition()
            {
                Filename = fileName,
                //crc = crc32.Compute(ms.ToArray()), 
            };

            fs.Upload(def, fileName, contentStream);
        }
    }
}
