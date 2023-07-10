/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
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
        public uint Size { get; set; }
        public uint Crc { get; set; }
    }

    /// <summary>
    /// Abstractions for Imlight's local KIWAD cache.
    /// </summary>
    public static class KiWadCache
    {
        // TODO: Make this configurable.
        private static readonly string _path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? string.Empty, $"cache");

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

            return file is null ? null : new Wad(file.OpenRead());
        }

        /// <summary>
        /// Gets all of the cached files from the FileStorage.
        /// </summary>
        /// <returns></returns>
        public static List<FileDefinition> GetAllCachedFiles()
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();

            var allFiles = fs.FindAll();
            return allFiles.Select(file => file.Id).ToList();
        }

        /// <summary>
        /// Caches a file into the local database.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="contentStream"></param>
        /// <param name="wad"></param>
        public static void CacheWad(string fileName, Wad wad)
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

            // Create a new FileDefinition. Create a new byte array from the content stream.
            var wadCrc = GetWadCRC(wad);
            var def = new FileDefinition
            {
                Filename = fileName,
                Size = wad.Size,
                Crc = wadCrc,
            };

            var contentStream = new MemoryStream(wad.GetData(), writable: false);
            fs.Upload(def, fileName, contentStream);
        }

        public static void DeleteWad(string fileName)
        {
            using var db = new LiteDatabase(_path);
            var fs = db.GetStorage<FileDefinition>();
            
            var file = fs.Find(f => f.Filename == fileName)
                .FirstOrDefault();

            if (file is not null) 
                return;
            
            Log.Logger.Warning($"LocalCache tried to delete a file [{fileName}] it did not contain!");
        }

        private static uint GetWadCRC(Wad wad)
        {
            // TEST: Marleybone-MB_Station-MB_Station_Hub.wad has a CRC32 hash of 2084731962.
            //                                           The header CRC32 hash is 3169958109.
            var data = wad.GetData();
            var wadHeaderSize = (int)wad.HeaderSize;
            var segmentSize = data.Length - wadHeaderSize;

            var wadMeatSegment = new byte[segmentSize];
            Buffer.BlockCopy(data, wadHeaderSize, wadMeatSegment, 0, segmentSize);
            var crc = crc32.Compute(wadMeatSegment);
            return crc;
        }
    }
}
