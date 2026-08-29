using System.Security.Cryptography;
using System.Text;
using Chronaiq.Application.Common.AI;
using Microsoft.Extensions.Options;

namespace Chronaiq.Infrastructure.AI;

/// <summary>
/// Produces stable, L2-normalized embeddings <b>without any external model</b>, so the ingest →
/// embed → vector-search pipeline is fully exercisable offline and in tests. Tokens are hashed
/// into the vector space and their contributions accumulated, giving a bag-of-tokens embedding
/// where texts sharing vocabulary land near each other under cosine distance — enough for the
/// similarity plumbing (HNSW index, <c>&lt;=&gt;</c> queries, graph linking) to behave realistically.
///
/// <para>
/// To use a real model (e.g. <c>text-embedding-3-small</c>), register an embedding client and
/// replace <see cref="EmbedAsync"/>'s body with a call to it — the surrounding contracts and the
/// 1536-dimension column stay identical.
/// </para>
/// </summary>
public sealed class DeterministicEmbeddingService(IOptions<AiOptions> options) : IEmbeddingService
{
    private readonly int _dimensions = options.Value.EmbeddingDimensions;

    public int Dimensions => _dimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[_dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(vector);
        }

        foreach (var token in Tokenize(text))
        {
            // Deterministically map each token to a dimension and a signed weight.
            var hash = HashToUInt32(token);
            var dimension = (int)(hash % (uint)_dimensions);
            var sign = (hash & 0x8000_0000) == 0 ? 1f : -1f;
            vector[dimension] += sign;
        }

        Normalize(vector);
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text)
        => text
            .ToLowerInvariant()
            .Split(
                [' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}', '/', '\\', '-'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1);

    private static uint HashToUInt32(string token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(token), hash);
        return BitConverter.ToUInt32(hash);
    }

    private static void Normalize(float[] vector)
    {
        double sumSquares = 0;
        foreach (var v in vector)
        {
            sumSquares += (double)v * v;
        }

        if (sumSquares <= 0)
        {
            return;
        }

        var magnitude = (float)Math.Sqrt(sumSquares);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
    }
}
