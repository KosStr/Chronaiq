using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A directed edge in the knowledge graph connecting two <see cref="BrainNode"/>s,
/// weighted by the cosine similarity that produced it. Composite primary key of
/// (<see cref="SourceNodeId"/>, <see cref="TargetNodeId"/>).
/// </summary>
public sealed class NodeLink : ICreatedTimestamp
{
    public Guid SourceNodeId { get; set; }
    public BrainNode? SourceNode { get; set; }

    public Guid TargetNodeId { get; set; }
    public BrainNode? TargetNode { get; set; }

    public double SimilarityScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
