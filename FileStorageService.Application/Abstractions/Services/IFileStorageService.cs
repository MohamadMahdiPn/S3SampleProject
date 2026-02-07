namespace FileStorageService.Application.Abstractions.Services;

public interface IFileStorageService
{
    Task<(string StoredKey, long Size, string Sha256)> SaveAsync(Stream stream, string tenantId,
        string originalFileName, string? contentType);

    (Stream Stream, string FullPath) OpenRead(string key);
    void Delete(string key);
    IEnumerable<string> ListKeys(string tenantId, string? prefix = null);
}