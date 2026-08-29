namespace Chronaiq.Application.Common.AI;

/// <summary>
/// Produces dense vector embeddings for text. Implemented in Infrastructure against
/// Semantic Kernel's embedding generator when a model is configured, with a deterministic
/// local fallback so the pipeline remains runnable offline / without credentials.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>Dimensionality of every vector returned, matching the <c>vector(1536)</c> column.</summary>
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
