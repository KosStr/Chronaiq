namespace Chronaiq.Infrastructure.Storage;

/// <summary>Binds the <c>Storage</c> configuration section.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Absolute or content-root-relative directory uploads are written under.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";

    /// <summary>URL prefix that <see cref="RootPath"/> is served from, recorded on each node.</summary>
    public string PublicBaseUrl { get; set; } = "/files";
}
