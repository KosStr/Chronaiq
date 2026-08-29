using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Features.Budget.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Chronaiq.Infrastructure.Analytics;

/// <summary>
/// Trains a linear regression (SDCA) over a budget's monthly spend history and projects the
/// next N months. With fewer than three data points there is not enough signal to fit a trend,
/// so it returns a flat carry-forward at the historical average and flags
/// <see cref="BudgetForecast.ModelTrained"/> as <c>false</c> rather than fabricating a slope.
/// </summary>
public sealed class MlNetBudgetForecastService : IBudgetForecastService
{
    private const int MinPointsToTrain = 3;

    public BudgetForecast Forecast(IReadOnlyList<MonthlySpendPoint> history, int horizonMonths)
    {
        horizonMonths = Math.Max(1, horizonMonths);

        if (history.Count == 0)
        {
            return new BudgetForecast([], MonthlyTrend: 0f, Intercept: 0f, ModelTrained: false);
        }

        var last = history[^1];

        if (history.Count < MinPointsToTrain)
        {
            var average = history.Average(p => p.TotalSpend);
            return FlatForecast(last, horizonMonths, average);
        }

        var ml = new MLContext(seed: 1);
        var trainingView = ml.Data.LoadFromEnumerable(
            history.Select(p => new SpendRow { MonthIndex = p.MonthIndex, TotalSpend = p.TotalSpend }));

        var pipeline = ml.Transforms
            .Concatenate("Features", nameof(SpendRow.MonthIndex))
            .Append(ml.Regression.Trainers.Sdca(
                labelColumnName: nameof(SpendRow.TotalSpend),
                featureColumnName: "Features",
                maximumNumberOfIterations: 200));

        var model = pipeline.Fit(trainingView);
        var engine = ml.Model.CreatePredictionEngine<SpendRow, SpendPrediction>(model);

        // Derive the fitted line from two evaluations so the trend can be explained to the user.
        var atZero = Predict(engine, 0);
        var atOne = Predict(engine, 1);
        var slope = atOne - atZero;

        var points = new List<BudgetForecastPoint>(horizonMonths);
        var cursor = new DateOnly(last.Year, last.Month, 1);
        for (var step = 1; step <= horizonMonths; step++)
        {
            cursor = cursor.AddMonths(1);
            var projected = Math.Max(0f, Predict(engine, last.MonthIndex + step));
            points.Add(new BudgetForecastPoint(cursor.Year, cursor.Month, projected));
        }

        return new BudgetForecast(points, MonthlyTrend: slope, Intercept: atZero, ModelTrained: true);
    }

    private static BudgetForecast FlatForecast(MonthlySpendPoint last, int horizonMonths, float level)
    {
        var points = new List<BudgetForecastPoint>(horizonMonths);
        var cursor = new DateOnly(last.Year, last.Month, 1);
        for (var step = 1; step <= horizonMonths; step++)
        {
            cursor = cursor.AddMonths(1);
            points.Add(new BudgetForecastPoint(cursor.Year, cursor.Month, Math.Max(0f, level)));
        }

        return new BudgetForecast(points, MonthlyTrend: 0f, Intercept: level, ModelTrained: false);
    }

    private static float Predict(PredictionEngine<SpendRow, SpendPrediction> engine, float monthIndex)
        => engine.Predict(new SpendRow { MonthIndex = monthIndex }).Score;

    private sealed class SpendRow
    {
        public float MonthIndex { get; set; }
        public float TotalSpend { get; set; }
    }

    private sealed class SpendPrediction
    {
        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
