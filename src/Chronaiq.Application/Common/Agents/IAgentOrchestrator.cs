namespace Chronaiq.Application.Common.Agents;

/// <summary>
/// Runs a single <see cref="AgentCommand"/> inside an isolated multi-agent orchestration
/// sandbox (Semantic Kernel today; swappable for Microsoft Agent Framework behind this
/// same contract). Progress, agent hand-offs, and streamed LLM tokens are surfaced to the
/// caller through <see cref="IAgentStreamNotifier"/> keyed on the command's correlation id.
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Executes the orchestration to completion. Implementations must be self-contained
    /// with respect to scope — they create their own DI scope per command — and must not
    /// throw for expected agent failures; those are reported via the notifier and returned
    /// in <see cref="AgentRunResult"/>.
    /// </summary>
    Task<AgentRunResult> RunAsync(AgentCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Terminal outcome of an orchestration run.</summary>
/// <param name="Succeeded">Whether the run completed without an unrecoverable error.</param>
/// <param name="Summary">Human-readable final answer or status.</param>
/// <param name="Error">Populated when <paramref name="Succeeded"/> is <c>false</c>.</param>
public sealed record AgentRunResult(bool Succeeded, string Summary, string? Error = null)
{
    public static AgentRunResult Ok(string summary) => new(true, summary);
    public static AgentRunResult Fail(string error) => new(false, "The agent run did not complete.", error);
}
