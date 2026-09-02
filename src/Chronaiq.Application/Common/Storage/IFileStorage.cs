namespace Chronaiq.Application.Common.Storage;

/// <summary>
/// Persists uploaded artifacts (documents, diagrams, voice notes, budget PDFs) and returns
/// a stable URL recorded on the owning <see cref="Domain.Entities.BrainNode"/>. The default
/// Infrastructure implementation writes to local disk under the content root; the contract
/// is intentionally storage-agnostic so it can be re-pointed at blob storage later.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves <paramref name="content"/> under a path derived from the owner and file name,
    /// returning the retrievable URL. Implementations must not assume the stream is
    /// seekable and must dispose nothing they did not create.
    /// </summary>
    Task<string> SaveAsync(
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}
