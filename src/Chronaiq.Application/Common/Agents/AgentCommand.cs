namespace Chronaiq.Application.Common.Agents;

/// <summary>
/// The kind of long-running work the multi-agent orchestrator should perform. The value
/// selects which specialist agents lead the <c>AgentGroupChat</c> session.
/// </summary>
public enum AgentTaskKind
{
    /// <summary>Ingest a freshly uploaded artifact: embed it and link it into the graph.</summary>
    IngestBrainNode = 0,

    /// <summary>Answer a free-form user prompt using all specialist agents.</summary>
    Conversation = 1,

    /// <summary>Re-flow the user's unscheduled tasks across the calendar.</summary>
    ScheduleTasks = 2,

    /// <summary>Produce a spending forecast / budget analysis.</summary>
    AnalyzeBudget = 3
}

/// <summary>
/// A unit of work enqueued by the API layer and later dequeued by the background worker.
/// This is the message that crosses the in-memory <see cref="Channels"/> pipeline, so it
/// is an immutable, self-contained record — it carries every identifier the worker needs
/// without reaching back into the request scope.
/// </summary>
/// <param name="CorrelationId">
/// Stable id used as the SignalR group name so the originating client receives this
/// command's event and token stream.
/// </param>
/// <param name="UserId">Owner of the data the agents will operate on.</param>
/// <param name="Kind">Which orchestration to run.</param>
/// <param name="Prompt">Free-form instruction for <see cref="AgentTaskKind.Conversation"/>.</param>
/// <param name="BrainNodeId">Target node for <see cref="AgentTaskKind.IngestBrainNode"/>.</param>
/// <param name="BudgetPlanId">Target plan for <see cref="AgentTaskKind.AnalyzeBudget"/>.</param>
/// <param name="EnqueuedAt">When the command entered the pipeline.</param>
public sealed record AgentCommand(
    Guid CorrelationId,
    Guid UserId,
    AgentTaskKind Kind,
    string? Prompt = null,
    Guid? BrainNodeId = null,
    Guid? BudgetPlanId = null,
    DateTimeOffset EnqueuedAt = default)
{
    public static AgentCommand Create(
        Guid userId,
        AgentTaskKind kind,
        string? prompt = null,
        Guid? brainNodeId = null,
        Guid? budgetPlanId = null) =>
        new(
            CorrelationId: Guid.CreateVersion7(),
            UserId: userId,
            Kind: kind,
            Prompt: prompt,
            BrainNodeId: brainNodeId,
            BudgetPlanId: budgetPlanId,
            EnqueuedAt: DateTimeOffset.UtcNow);
}
