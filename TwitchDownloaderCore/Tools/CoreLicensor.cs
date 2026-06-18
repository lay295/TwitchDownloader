using System;
using System.IO;
using System.Security.Cryptography;

namespace TwitchDownloaderCore.Tools
{
    // This file should not be modified without explicit permission
    public static class CoreLicensor
    {
        public static void EnsureFilesExist(string baseDir)
        {
            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            }

            using (var ms = new MemoryStream(Properties.Resources.copyright))
            {
                var filePath = Path.Combine(baseDir, "COPYRIGHT.txt");
                TryCopyIfDifferent(ms, filePath);
            }

            using (var ms = new MemoryStream(Properties.Resources.third_party_licenses))
            {
                var filePath = Path.Combine(baseDir, "THIRD-PARTY-LICENSES.txt");
                TryCopyIfDifferent(ms, filePath);
            }
        }

        private static void TryCopyIfDifferent(Stream resourceStream, string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();

                var resourceHash = sha256.ComputeHash(resourceStream);

                byte[] fileHash;
                using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
                {
                    fileHash = sha256.ComputeHash(fs);
                }

                if (!resourceHash.AsSpan().SequenceEqual(fileHash))
                {
                    resourceStream.Seek(0, SeekOrigin.Begin);
                    using var fs = File.Create(filePath);
                    resourceStream.CopyTo(fs);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}