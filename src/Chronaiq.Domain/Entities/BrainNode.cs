using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A single unit of ingested knowledge in the user's "Second Brain": a document,
/// diagram, voice note, or parsed budget PDF. The <see cref="Embedding"/> is populated
/// asynchronously by the ingestion pipeline, which is why it is nullable — a node exists
/// (and is returned by the API) before its vector has been computed.
/// </summary>
public sealed class BrainNode : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required string Title { get; set; }
    public required string Content { get; set; }

    /// <summary>One of the <see cref="Enums.BrainSourceType"/> values.</summary>
    public required string SourceType { get; set; }

    public string? RawFileUrl { get; set; }

    /// <summary>
    /// A 1536-dimension embedding (matching <c>text-embedding-3-small</c>). Stored in the
    /// <c>vector(1536)</c> pgvector column. Exposed as <see cref="float"/>[] so the domain
    /// carries no dependency on the pgvector library; the mapping to
    /// <c>Pgvector.Vector</c> lives in the Infrastructure configuration.
    /// </summary>
    public float[]? Embedding { get; set; }

    // Knowledge-graph edges where this node is the source / target.
    public ICollection<NodeLink> OutgoingLinks { get; } = new List<NodeLink>();
    public ICollection<NodeLink> IncomingLinks { get; } = new List<NodeLink>();
    public ICollection<TaskReference> TaskReferences { get; } = new List<TaskReference>();
}
