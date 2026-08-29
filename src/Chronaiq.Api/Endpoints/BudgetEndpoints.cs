using Chronaiq.Application.Features.Budget;
using MediatR;

namespace Chronaiq.Api.Endpoints;

/// <summary>Budget plan, ledger, and ML.NET forecast endpoints.</summary>
public static class BudgetEndpoints
{
    public sealed record CreateBudgetPlanRequest(
        string Name,
        decimal MonthlyIncome,
        decimal MonthlySavingsTarget,
        DateOnly StartDate,
        DateOnly? EndDate);

    public sealed record AddTransactionRequest(
        decimal Amount,
        string MerchantName,
        string Category,
        DateTimeOffset TransactionDate);

    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users/{userId:guid}/budgets").WithTags("Budget");

        users.MapPost("/", async (Guid userId, CreateBudgetPlanRequest request, ISender sender, CancellationToken ct) =>
        {
            var plan = await sender.Send(new CreateBudgetPlanCommand(
                userId,
                request.Name,
                request.MonthlyIncome,
                request.MonthlySavingsTarget,
                request.StartDate,
                request.EndDate), ct);

            return Results.Created($"/api/budgets/{plan.Id}", plan);
        })
        .WithName("CreateBudgetPlan");

        var plans = app.MapGroup("/api/budgets/{budgetPlanId:guid}").WithTags("Budget");

        plans.MapPost("/transactions", async (
            Guid budgetPlanId,
            AddTransactionRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var transaction = await sender.Send(new AddTransactionCommand(
                budgetPlanId,
                request.Amount,
                request.MerchantName,
                request.Category,
                request.TransactionDate), ct);

            return Results.Created($"/api/budgets/{budgetPlanId}/transactions/{transaction.Id}", transaction);
        })
        .WithName("AddTransaction");

        plans.MapGet("/forecast", async (
            Guid budgetPlanId,
            int? horizon,
            ISender sender,
            CancellationToken ct) =>
        {
            var forecast = await sender.Send(new ForecastBudgetQuery(budgetPlanId, horizon ?? 3), ct);
            return Results.Ok(forecast);
        })
        .WithName("ForecastBudget");

        return app;
    }
}
