using Chronaiq.Domain.Common;

namespace Chronaiq.Domain.Entities;

/// <summary>
/// A single ledger entry belonging to a <see cref="BudgetPlan"/>. Positive
/// <see cref="Amount"/> values are spend; the Finance agent's ML.NET forecaster is
/// trained over the history of these rows.
/// </summary>
public sealed class Transaction : EntityBase, ICreatedTimestamp
{
    public Guid BudgetPlanId { get; set; }
    public BudgetPlan? BudgetPlan { get; set; }

    public decimal Amount { get; set; }
    public required string MerchantName { get; set; }
    public required string Category { get; set; }

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
