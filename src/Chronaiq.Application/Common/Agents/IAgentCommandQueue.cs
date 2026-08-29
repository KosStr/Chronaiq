namespace Chronaiq.Application.Common.Agents;

/// <summary>
/// The producer/consumer boundary for the in-memory command pipeline. The API layer
/// <see cref="EnqueueAsync"/>s a command and immediately returns <c>202 Accepted</c>; the
/// hosted worker consumes the <see cref="Reader"/>. Backed by
/// <see cref="System.Threading.Channels.Channel{T}"/> in Infrastructure.
/// </summary>
public interface IAgentCommandQueue
{
    /// <summary>
    /// Enqueues a command for background execution. Awaits only if the bounded channel is
    /// momentarily full, applying natural back-pressure to the API rather than unboundedly
    /// buffering work.
    /// </summary>
    ValueTask EnqueueAsync(AgentCommand command, CancellationToken cancellationToken = default);

    /// <summary>Stream of queued commands for the background worker to drain.</summary>
    IAsyncEnumerable<AgentCommand> Reader { get; }
}
