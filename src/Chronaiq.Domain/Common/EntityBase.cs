namespace Chronaiq.Domain.Common;

/// <summary>
/// Base type for all aggregate roots and entities that use a surrogate GUID key.
/// The identifier is assigned in application code (via <see cref="Guid.CreateVersion7"/>)
/// so that entities are fully usable before they are persisted, and so that inserts
/// remain index-friendly thanks to the time-ordered nature of UUIDv7.
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

/// <summary>Marks an entity that records when it was first created.</summary>
public interface ICreatedTimestamp
{
    DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Marks an entity that tracks the time of its most recent modification.</summary>
public interface IUpdatedTimestamp
{
    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Convenience base for entities that carry both created and updated stamps.</summary>
public abstract class AuditableEntity : EntityBase, ICreatedTimestamp, IUpdatedTimestamp
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
