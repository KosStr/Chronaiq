using Chronaiq.Domain.Common;
using Chronaiq.Domain.Enums;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A person using Chronaiq. The profile fields (<see cref="Chronotype"/>,
/// <see cref="PreferredWorkStart"/>, <see cref="PreferredWorkEnd"/>) are the primary
/// inputs the Schedule agent uses to place tasks into the calendar.
/// </summary>
public sealed class User : AuditableEntity
{
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public Chronotype Chronotype { get; set; } = Chronotype.MorningLark;

    /// <summary>Local wall-clock start of the user's preferred working window.</summary>
    public TimeOnly PreferredWorkStart { get; set; } = new(9, 0);

    /// <summary>Local wall-clock end of the user's preferred working window.</summary>
    public TimeOnly PreferredWorkEnd { get; set; } = new(17, 0);

    // Navigation properties.
    public ICollection<BrainNode> BrainNodes { get; } = new List<BrainNode>();
    public ICollection<CalTask> Tasks { get; } = new List<CalTask>();
    public ICollection<BudgetPlan> BudgetPlans { get; } = new List<BudgetPlan>();
}
