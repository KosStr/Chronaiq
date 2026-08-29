using Chronaiq.Application.Features.BrainNodes.Models;

namespace Chronaiq.Application.Common.Persistence;

/// <summary>
/// Vector-similarity access over <c>"BrainNodes"</c>. Implemented in Infrastructure with
/// pgvector's cosine-distance operator (<c>&lt;=&gt;</c>) against the HNSW index, kept
/// separate from <see cref="IApplicationDbContext"/> because the query cannot be expressed
/// in provider-agnostic LINQ.
/// </summary>
public interface IBrainNodeSearch
{
    /// <summary>
    /// Returns the <paramref name="limit"/> nearest nodes to <paramref name="queryEmbedding"/>
    /// for the given user, ordered by ascending cosine distance (nearest first). Nodes whose
    /// embedding has not yet been computed are excluded.
    /// </summary>
    Task<IReadOnlyList<BrainNodeMatch>> SearchAsync(
        Guid userId,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken = default);
}
