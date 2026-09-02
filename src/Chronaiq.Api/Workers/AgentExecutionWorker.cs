using Chronaiq.Application.Common.Agents;

namespace Chronaiq.Api.Workers;

/// <summary>
/// The background service that drains the in-memory command pipeline and runs each command
/// through the multi-agent orchestrator. This is the consumer half of the pattern the API's
/// HTTP 202 flow depends on: web requests enqueue and return immediately; this worker executes
/// out of band. Failures in a single command are logged and swallowed so the loop keeps draining
/// (the orchestrator also reports failures to the client over SignalR).
/// </summary>
public sealed class AgentExecutionWorker(
    IAgentCommandQueue queue,
    IAgentOrchestrator orchestrator,
    ILogger<AgentExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agent execution worker started.");

        try
        {
            await foreach (var command in queue.Reader.WithCancellation(stoppingToken))
            {
                await ProcessAsync(command, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Agent execution worker stopping.");
    }

    private async Task ProcessAsync(AgentCommand command, CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation(
                "Executing agent command {CorrelationId} ({Kind}) for user {UserId}",
                command.CorrelationId, command.Kind, command.UserId);

            var result = await orchestrator.RunAsync(command, stoppingToken);

            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Agent command {CorrelationId} completed with failure: {Error}",
                    command.CorrelationId, result.Error);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw; // propagate shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error executing agent command {CorrelationId}", command.CorrelationId);
        }
    }
}
