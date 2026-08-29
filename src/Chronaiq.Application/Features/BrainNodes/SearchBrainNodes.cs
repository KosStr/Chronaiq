using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.BrainNodes.Models;
using MediatR;

namespace Chronaiq.Application.Features.BrainNodes;

/// <summary>
/// Semantic search across a user's Second Brain: embeds the query text, then finds the
/// nearest nodes by pgvector cosine distance. Returns fewer than <see cref="Limit"/> results
/// when the user has fewer embedded nodes.
/// </summary>
public sealed record SearchBrainNodesQuery(Guid UserId, string Query, int Limit = 10)
    : IRequest<IReadOnlyList<BrainNodeMatch>>;

public sealed class SearchBrainNodesHandler(
    IEmbeddingService embeddings,
    IBrainNodeSearch search)
    : IRequestHandler<SearchBrainNodesQuery, IReadOnlyList<BrainNodeMatch>>
{
    public async Task<IReadOnlyList<BrainNodeMatch>> Handle(
        SearchBrainNodesQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        var limit = Math.Clamp(request.Limit, 1, 50);
        var queryEmbedding = await embeddings.EmbedAsync(request.Query, cancellationToken);

        return await search.SearchAsync(request.UserId, queryEmbedding, limit, cancellationToken);
    }
}
