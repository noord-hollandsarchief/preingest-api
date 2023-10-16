using System;
using System.IO;
using System.Reflection;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// Helper class for date/time format
    /// </summary>
    public static class LinkerHelper
    {
        /// <summary>
        /// Gets the linker timestamp UTC.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public static DateTime GetLinkerTimestampUtc(Assembly assembly)
        {
            var location = assembly.Location;
            return GetLinkerTimestampUtc(location);
        }

        /// <summary>
        /// Gets the linker timestamp UTC.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns></returns>
        public static DateTime GetLinkerTimestampUtc(string filePath)
        {
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var bytes = new byte[2048];

            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Read(bytes, 0, bytes.Length);
            }

            var headerPos = BitConverter.ToInt32(bytes, peHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(bytes, headerPos + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return dt.AddSeconds(secondsSince1970);
        }
    }
}
