using Chronaiq.Domain.Entities;

namespace Chronaiq.Application.Features.Budget.Models;

/// <summary>Read model for a budget plan.</summary>
public sealed record BudgetPlanDto(
    Guid Id,
    Guid UserId,
    string Name,
    decimal MonthlyIncome,
    decimal MonthlySavingsTarget,
    bool IsActive,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    public static BudgetPlanDto FromEntity(BudgetPlan plan) => new(
        plan.Id,
        plan.UserId,
        plan.Name,
        plan.MonthlyIncome,
        plan.MonthlySavingsTarget,
        plan.IsActive,
        plan.StartDate,
        plan.EndDate);
}

/// <summary>Read model for a ledger entry.</summary>
public sealed record TransactionDto(
    Guid Id,
    Guid BudgetPlanId,
    decimal Amount,
    string MerchantName,
    string Category,
    DateTimeOffset TransactionDate)
{
    public static TransactionDto FromEntity(Transaction t) => new(
        t.Id,
        t.BudgetPlanId,
        t.Amount,
        t.MerchantName,
        t.Category,
        t.TransactionDate);
}
