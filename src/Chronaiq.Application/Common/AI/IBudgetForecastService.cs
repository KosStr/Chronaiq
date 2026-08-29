using Chronaiq.Application.Features.Budget.Models;

namespace Chronaiq.Application.Common.AI;

/// <summary>
/// Time-series / regression forecasting over a budget's transaction history. Implemented
/// with ML.NET in Infrastructure. Kept behind an interface so the Application layer can be
/// unit-tested with a stub and so the modelling library never leaks into handlers.
/// </summary>
public interface IBudgetForecastService
{
    /// <summary>
    /// Trains an on-the-fly model over <paramref name="history"/> and projects spend for the
    /// next <paramref name="horizonMonths"/> months. Returns a degenerate (flat) forecast
    /// when there is too little history to fit a model, rather than throwing.
    /// </summary>
    BudgetForecast Forecast(
        IReadOnlyList<MonthlySpendPoint> history,
        int horizonMonths);
}
