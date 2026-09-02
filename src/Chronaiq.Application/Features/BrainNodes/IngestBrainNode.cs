using Chronaiq.Application.Common.Agents;
using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Common.Storage;
using Chronaiq.Domain.Entities;
using Chronaiq.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.BrainNodes;

/// <summary>
/// First half of the ingestion flow that runs <b>synchronously</b> inside the web request:
/// it persists the raw file, creates a <see cref="BrainNode"/> in a pending (no-embedding)
/// state, and enqueues an <see cref="AgentCommand"/> for the background worker to embed and
/// graph-link. The API returns <c>202 Accepted</c> with the correlation id so the client can
/// subscribe to progress over SignalR. The heavy work (embedding, similarity linking) is
/// deliberately <i>not</i> done here.
/// </summary>
public sealed record IngestBrainNodeCommand(
    Guid UserId,
    string Title,
    string Content,
    string SourceType,
    Stream? FileContent = null,
    string? FileName = null) : IRequest<IngestAcceptedDto>;

/// <summary>Acknowledgement returned to the client alongside the HTTP 202.</summary>
public sealed record IngestAcceptedDto(Guid BrainNodeId, Guid CorrelationId, string Status);

public sealed class IngestBrainNodeHandler(
    IApplicationDbContext db,
    IFileStorage fileStorage,
    IAgentCommandQueue queue)
    : IRequestHandler<IngestBrainNodeCommand, IngestAcceptedDto>
{
    public async Task<IngestAcceptedDto> Handle(IngestBrainNodeCommand request, CancellationToken cancellationToken)
    {
        var sourceType = BrainSourceType.IsKnown(request.SourceType)
            ? request.SourceType
            : throw new ArgumentException(
                $"Unknown source type '{request.SourceType}'. Expected one of: {string.Join(", ", BrainSourceType.All)}.",
                nameof(request));

        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        string? rawFileUrl = null;
        if (request.FileContent is not null && !string.IsNullOrWhiteSpace(request.FileName))
        {
            rawFileUrl = await fileStorage.SaveAsync(
                request.UserId, request.FileName, request.FileContent, cancellationToken);
        }

        var node = new BrainNode
        {
            UserId = request.UserId,
            Title = request.Title.Trim(),
            Content = request.Content,
            SourceType = sourceType,
            RawFileUrl = rawFileUrl,
            Embedding = null // computed asynchronously by the ingestion agent.
        };

        db.BrainNodes.Add(node);
        await db.SaveChangesAsync(cancellationToken);

        // Hand off to the in-memory pipeline; the worker will embed and link this node.
        var command = AgentCommand.Create(
            request.UserId,
            AgentTaskKind.IngestBrainNode,
            brainNodeId: node.Id);

        await queue.EnqueueAsync(command, cancellationToken);

        return new IngestAcceptedDto(node.Id, command.CorrelationId, "Accepted");
    }
}
