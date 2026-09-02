using Chronaiq.Application.Common.Agents;

namespace Chronaiq.Api.Endpoints;

/// <summary>
/// Fire-and-subscribe endpoints for the multi-agent pipeline. Each enqueues an
/// <see cref="AgentCommand"/> and returns <c>202 Accepted</c> with the correlation id; the
/// client then joins the SignalR group for that id (<c>AgentHub.Subscribe</c>) to receive live
/// agent activations, token streams, milestones, and the final result.
/// </summary>
public static class AgentEndpoints
{
    public sealed record ConverseRequest(string Prompt);

    public sealed record AnalyzeBudgetRequest(Guid? BudgetPlanId);

    private sealed record Accepted(Guid CorrelationId, string Status);

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/{userId:guid}/agents").WithTags("Agents");

        group.MapPost("/converse", async (
            Guid userId,
            ConverseRequest request,
            IAgentCommandQueue queue,
            CancellationToken ct) =>
        {
            var command = AgentCommand.Create(userId, AgentTaskKind.Conversation, prompt: request.Prompt);
            await queue.EnqueueAsync(command, ct);
            return Results.Accepted(value: new Accepted(command.CorrelationId, "Accepted"));
        })
        .WithName("AgentConverse");

        group.MapPost("/analyze-budget", async (
            Guid userId,
            AnalyzeBudgetRequest? request,
            IAgentCommandQueue queue,
            CancellationToken ct) =>
        {
            var command = AgentCommand.Create(
                userId, AgentTaskKind.AnalyzeBudget, budgetPlanId: request?.BudgetPlanId);
            await queue.EnqueueAsync(command, ct);
            return Results.Accepted(value: new Accepted(command.CorrelationId, "Accepted"));
        })
        .WithName("AgentAnalyzeBudget");

        group.MapPost("/schedule", async (
            Guid userId,
            IAgentCommandQueue queue,
            CancellationToken ct) =>
        {
            var command = AgentCommand.Create(userId, AgentTaskKind.ScheduleTasks);
            await queue.EnqueueAsync(command, ct);
            return Results.Accepted(value: new Accepted(command.CorrelationId, "Accepted"));
        })
        .WithName("AgentSchedule");

        return app;
    }
}
