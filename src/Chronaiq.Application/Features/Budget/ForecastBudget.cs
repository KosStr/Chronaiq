using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Budget.Models;
using Chronaiq.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Budget;

/// <summary>
/// Projects future monthly spend for a budget plan. Aggregates the plan's transactions into
/// monthly totals and hands them to the ML.NET regression forecaster.
/// </summary>
public sealed record ForecastBudgetQuery(Guid BudgetPlanId, int HorizonMonths = 3)
    : IRequest<BudgetForecast>;

public sealed class ForecastBudgetHandler(
    IApplicationDbContext db,
    IBudgetForecastService forecaster)
    : IRequestHandler<ForecastBudgetQuery, BudgetForecast>
{
    public async Task<BudgetForecast> Handle(ForecastBudgetQuery request, CancellationToken cancellationToken)
    {
        var planExists = await db.BudgetPlans.AnyAsync(p => p.Id == request.BudgetPlanId, cancellationToken);
        if (!planExists)
        {
            throw new NotFoundException(nameof(BudgetPlan), request.BudgetPlanId);
        }

        // Pull the raw (year, month, amount) triples; aggregation happens in memory so the
        // month ordinal is contiguous even across gap months with no spend.
        var rows = await db.Transactions
            .Where(t => t.BudgetPlanId == request.BudgetPlanId)
            .Select(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Amount })
            .ToListAsync(cancellationToken);

        var history = BuildMonthlyHistory(rows.Select(r => (r.Year, r.Month, r.Amount)));

        var horizon = Math.Clamp(request.HorizonMonths, 1, 24);
        return forecaster.Forecast(history, horizon);
    }

    /// <summary>
    /// Collapses transactions into a dense, gap-filled monthly series ordered from the first
    /// month that had activity to the last. Missing months are emitted as zero-spend points so
    /// the regression sees a real time axis rather than a compressed one.
    /// </summary>
    private static IReadOnlyList<MonthlySpendPoint> BuildMonthlyHistory(
        IEnumerable<(int Year, int Month, decimal Amount)> rows)
    {
        var totals = new Dictionary<(int Year, int Month), decimal>();
        foreach (var (year, month, amount) in rows)
        {
            var key = (year, month);
            totals[key] = totals.GetValueOrDefault(key) + amount;
        }

        if (totals.Count == 0)
        {
            return [];
        }

        var ordered = totals.Keys
            .OrderBy(k => k.Year)
            .ThenBy(k => k.Month)
            .ToList();

        var first = ordered[0];
        var last = ordered[^1];

        var series = new List<MonthlySpendPoint>();
        var index = 0;
        for (var cursor = new DateOnly(first.Year, first.Month, 1);
             cursor <= new DateOnly(last.Year, last.Month, 1);
             cursor = cursor.AddMonths(1))
        {
            var spend = totals.GetValueOrDefault((cursor.Year, cursor.Month));
            series.Add(new MonthlySpendPoint(index++, cursor.Year, cursor.Month, (float)spend));
        }

        return series;
    }
}
