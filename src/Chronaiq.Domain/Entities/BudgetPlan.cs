using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A user's budget for a period. Aggregates <see cref="Transactions"/> and provides the
/// income/savings targets the Finance agent forecasts against.
/// </summary>
public sealed class BudgetPlan : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public required string Name { get; set; }

    public decimal MonthlyIncome { get; set; }
    public decimal MonthlySavingsTarget { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public ICollection<Transaction> Transactions { get; } = new List<Transaction>();
}
