using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A schedulable unit of work. <see cref="EnergyRequirement"/> (1–5) is matched against
/// the user's chronotype and preferred working window by the Schedule agent to choose a
/// <see cref="ScheduledStart"/>/<see cref="ScheduledEnd"/>.
/// </summary>
public sealed class CalTask : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Cognitive load on a 1 (trivial) to 5 (deep focus) scale.</summary>
    public int EnergyRequirement { get; set; } = 3;

    public int DurationMinutes { get; set; } = 30;

    public DateTimeOffset? ScheduledStart { get; set; }
    public DateTimeOffset? ScheduledEnd { get; set; }

    public bool IsCompleted { get; set; }

    public ICollection<TaskReference> References { get; } = new List<TaskReference>();
}
