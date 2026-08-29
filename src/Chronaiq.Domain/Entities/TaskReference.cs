using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// Many-to-many bridge linking a <see cref="CalTask"/> to the <see cref="BrainNode"/>s
/// that provide its context (notes, diagrams, reference material). Composite primary key
/// of (<see cref="TaskId"/>, <see cref="BrainNodeId"/>).
/// </summary>
public sealed class TaskReference : ICreatedTimestamp
{
    public Guid TaskId { get; set; }
    public CalTask? Task { get; set; }

    public Guid BrainNodeId { get; set; }
    public BrainNode? BrainNode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
