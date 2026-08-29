using Chronaiq.Api.Hubs;
using Chronaiq.Application.Common.Agents;
using Microsoft.AspNetCore.SignalR;

namespace Chronaiq.Api.Realtime;

/// <summary>
/// SignalR implementation of the Application-layer <see cref="IAgentStreamNotifier"/>. Lives in
/// the API project (the only layer aware of ASP.NET Core) and fans every event out to the hub
/// group that corresponds to the command's correlation id. Client event names are fixed here so
/// front-ends can bind to them: <c>AgentActivated</c>, <c>Token</c>, <c>Milestone</c>, <c>Completed</c>.
/// </summary>
public sealed class SignalRAgentStreamNotifier(IHubContext<AgentHub> hub) : IAgentStreamNotifier
{
    public Task AgentActivatedAsync(Guid correlationId, string agentName, CancellationToken cancellationToken = default)
        => hub.Clients.Group(AgentHub.GroupName(correlationId))
            .SendAsync("AgentActivated", new { correlationId, agentName }, cancellationToken);

    public Task TokenAsync(Guid correlationId, string agentName, string token, CancellationToken cancellationToken = default)
        => hub.Clients.Group(AgentHub.GroupName(correlationId))
            .SendAsync("Token", new { correlationId, agentName, token }, cancellationToken);

    public Task MilestoneAsync(Guid correlationId, string message, CancellationToken cancellationToken = default)
        => hub.Clients.Group(AgentHub.GroupName(correlationId))
            .SendAsync("Milestone", new { correlationId, message }, cancellationToken);

    public Task CompletedAsync(Guid correlationId, AgentRunResult result, CancellationToken cancellationToken = default)
        => hub.Clients.Group(AgentHub.GroupName(correlationId))
            .SendAsync("Completed", new { correlationId, result.Succeeded, result.Summary, result.Error }, cancellationToken);
}
