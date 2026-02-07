using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FileStorageService.Application.Contracts.FileStorageServices
{
    public class FileStorageService
    {
        #region Constructor
        private readonly IConfiguration _config;
        public FileStorageService(IConfiguration config) => _config = config;
        #endregion


        #region RootPath
        public string RootPath => _config["Storage:RootPath"] ?? "storage";
        #endregion

        #region SaveAsync
        public async Task<(string StoredKey, long Size, string Sha256)> SaveAsync(Stream stream, string tenantId, string originalFileName, string? contentType)
        {
            Directory.CreateDirectory(RootPath);

            var ext = Path.GetExtension(originalFileName);
            if (ext.Length > 10) ext = ""; // ساده‌سازی امنیتی

            var key = $"{tenantId}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(RootPath, key.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            using var sha = SHA256.Create();
            byte[] buffer = new byte[81920];
            int read;
            long total = 0;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                sha.TransformBlock(buffer, 0, read, null, 0);
                total += read;
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();

            return (key, total, hash);
        }
        #endregion

        #region OpenRead

        public (Stream Stream, string FullPath) OpenRead(string key)
        {
            var fullPath = Path.Combine(RootPath, key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) throw new FileNotFoundException("File not found.", key);

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return (stream, fullPath);
        }

        #endregion

        #region Delete

        public void Delete(string key)
        {
            var fullPath = Path.Combine(RootPath, key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }

        #endregion

        #region ListKeys

        public IEnumerable<string> ListKeys(string tenantId, string? prefix = null)
        {
            var basePath = Path.Combine(RootPath, tenantId.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(basePath)) return Enumerable.Empty<string>();

            var files = Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories);

            var keys = files.Select(f =>
            {
                var rel = Path.GetRelativePath(RootPath, f);
                return rel.Replace(Path.DirectorySeparatorChar, '/');
            });

            if (!string.IsNullOrWhiteSpace(prefix))
                keys = keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            return keys;
        }


        #endregion

    }
}
