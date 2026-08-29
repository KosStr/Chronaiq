namespace Chronaiq.Application.Features.BrainNodes.Models;

/// <summary>Read model for a Second Brain node returned to API clients.</summary>
public sealed record BrainNodeDto(
    Guid Id,
    Guid UserId,
    string Title,
    string SourceType,
    string? RawFileUrl,
    bool HasEmbedding,
    DateTimeOffset CreatedAt);

/// <summary>
/// A single hit from a vector-similarity search, carrying the pgvector cosine
/// <see cref="Distance"/> (0 = identical). <see cref="Similarity"/> is the convenience
/// complement (<c>1 - distance</c>) more natural for ranking display.
/// </summary>
public sealed record BrainNodeMatch(
    Guid Id,
    string Title,
    string SourceType,
    double Distance)
{
    public double Similarity => 1d - Distance;
}
