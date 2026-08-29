namespace Chronaiq.Application.Common.Agents;

/// <summary>
/// Real-time channel back to the originating user. Implemented in the API layer over
/// SignalR (<c>IHubContext</c>), it is abstracted here so orchestration logic in
/// Infrastructure stays free of any ASP.NET Core dependency. Every method is keyed by the
/// command <paramref name="correlationId"/>, which the SignalR implementation maps to a
/// hub group the client joined when it issued the request.
/// </summary>
public interface IAgentStreamNotifier
{
    /// <summary>Signals that a specialist agent has taken the floor in the group chat.</summary>
    Task AgentActivatedAsync(Guid correlationId, string agentName, CancellationToken cancellationToken = default);

    /// <summary>Pushes an incremental LLM token / text delta to the client.</summary>
    Task TokenAsync(Guid correlationId, string agentName, string token, CancellationToken cancellationToken = default);

    /// <summary>Reports a discrete, structured milestone (e.g. "embedded", "linked 4 nodes").</summary>
    Task MilestoneAsync(Guid correlationId, string message, CancellationToken cancellationToken = default);

    /// <summary>Terminal notification carrying the final result of the run.</summary>
    Task CompletedAsync(Guid correlationId, AgentRunResult result, CancellationToken cancellationToken = default);
}
