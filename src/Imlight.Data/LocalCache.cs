using System;
using System.Linq;
using System.IO;
using LiteDB;

namespace Imlight.Data
{
    public class _files
    {
        public string Id { get; set; }
        public string filename { get; set; }
        public long length { get; set; }
        public int chunks { get; set; }
        public uint crc { get; set; }
    }

    public static class LocalCache
    {
        private static readonly string _path = $@"{Directory.GetCurrentDirectory()}/.cache.db";

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

        public static byte[] GetCachedFile(string fileName)
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<_files>();

            var file = fs.Find(f => f.Filename == fileName)
                .FirstOrDefault();

            if (file is null)
                throw new Exception($"No file by {fileName} was found!");

            var memStream = new MemoryStream();
            file.CopyTo(memStream);

            return memStream.ToArray();
        }
    }
}
