using Microsoft.AspNetCore.SignalR;

namespace Chronaiq.Api.Hubs;

/// <summary>
/// Real-time channel to clients. When a client issues a command it receives a correlation id in
/// the HTTP 202 response, then calls <see cref="Subscribe"/> with that id to join a per-command
/// group. The orchestrator (via <c>SignalRAgentStreamNotifier</c>) pushes agent activations,
/// token deltas, milestones, and the final result to exactly that group.
/// </summary>
public sealed class AgentHub : Hub
{
    /// <summary>Client name of the group-membership method (kept in sync with the notifier).</summary>
    public static string GroupName(Guid correlationId) => $"agent-run:{correlationId}";

    /// <summary>Joins the caller to the group for a specific agent run.</summary>
    public Task Subscribe(Guid correlationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(correlationId));

    /// <summary>Leaves the group for a specific agent run.</summary>
    public Task Unsubscribe(Guid correlationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(correlationId));
}
