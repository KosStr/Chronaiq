namespace Chronaiq.Application.Features.Budget.Models;

/// <summary>Aggregated spend for a single calendar month, the unit the forecaster is trained on.</summary>
/// <param name="MonthIndex">
/// Zero-based ordinal of the month within the training window. Used as the regression
/// feature (x); keeping it an ordinal rather than a date makes the model horizon-agnostic.
/// </param>
/// <param name="Year">Calendar year of the point (for labelling).</param>
/// <param name="Month">Calendar month, 1–12 (for labelling).</param>
/// <param name="TotalSpend">Sum of transaction amounts in the month.</param>
public sealed record MonthlySpendPoint(int MonthIndex, int Year, int Month, float TotalSpend);

/// <summary>A single projected month produced by the forecaster.</summary>
public sealed record BudgetForecastPoint(int Year, int Month, float ProjectedSpend);

/// <summary>
/// Output of <see cref="Common.AI.IBudgetForecastService"/>: the projected months plus the
/// fitted trend so callers can explain the projection ("spend rising ~$120/mo").
/// </summary>
/// <param name="Points">Projected spend per future month.</param>
/// <param name="MonthlyTrend">Fitted slope: average change in spend per month.</param>
/// <param name="Intercept">Fitted intercept of the trend line.</param>
/// <param name="ModelTrained">
/// <c>false</c> when there was insufficient history to fit a regression and a flat
/// carry-forward projection was returned instead.
/// </param>
public sealed record BudgetForecast(
    IReadOnlyList<BudgetForecastPoint> Points,
    float MonthlyTrend,
    float Intercept,
    bool ModelTrained);
