using Chronaiq.Application.Common.Storage;
using Microsoft.Extensions.Options;

namespace Chronaiq.Infrastructure.Storage;

/// <summary>
/// Writes uploaded artifacts to the local filesystem under a per-user folder and returns a
/// public URL of the form <c>{PublicBaseUrl}/{userId}/{unique}_{fileName}</c>. Suitable for a
/// prototype / single-node deployment; the <see cref="IFileStorage"/> abstraction lets this be
/// swapped for blob storage without touching the Application layer.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<string> SaveAsync(
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var safeName = SanitizeFileName(fileName);
        var unique = Guid.CreateVersion7().ToString("n");
        var storedName = $"{unique}_{safeName}";

        var userFolder = Path.Combine(GetRootPath(), userId.ToString("n"));
        Directory.CreateDirectory(userFolder);

        var fullPath = Path.Combine(userFolder, storedName);
        await using (var target = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{userId:n}/{storedName}";
    }

    private string GetRootPath()
        => Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(AppContext.BaseDirectory, _options.RootPath);

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "upload" : cleaned;
    }
}
