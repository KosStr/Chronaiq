using System.Threading.Channels;
using Chronaiq.Application.Common.Agents;

namespace Chronaiq.Infrastructure.Messaging;

/// <summary>
/// In-memory producer/consumer pipeline backed by a bounded
/// <see cref="Channel{T}"/>. Registered as a singleton so the API (producer) and the hosted
/// worker (consumer) share one instance. The channel is bounded to apply back-pressure:
/// if the worker falls behind, <see cref="EnqueueAsync"/> asynchronously waits for capacity
/// instead of letting the queue grow without limit.
/// </summary>
public sealed class ChannelAgentCommandQueue : IAgentCommandQueue
{
    private readonly Channel<AgentCommand> _channel;

    public ChannelAgentCommandQueue(int capacity = 256)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // exactly one hosted worker drains the channel.
            SingleWriter = false  // many concurrent web requests may enqueue.
        };

        _channel = Channel.CreateBounded<AgentCommand>(options);
    }

    public ValueTask EnqueueAsync(AgentCommand command, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(command, cancellationToken);

    public IAsyncEnumerable<AgentCommand> Reader => _channel.Reader.ReadAllAsync();
}
