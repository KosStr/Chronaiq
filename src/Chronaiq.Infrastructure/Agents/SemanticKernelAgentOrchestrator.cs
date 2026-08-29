using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Common.Agents;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Budget;
using Chronaiq.Application.Features.Tasks;
using Chronaiq.Domain.Entities;
using Chronaiq.Infrastructure.AI;
using Chronaiq.Infrastructure.Agents.Plugins;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Chronaiq.Infrastructure.Agents;

/// <summary>
/// Runs an <see cref="AgentCommand"/> inside its own DI scope, coordinating the specialist
/// agents. Deterministic, data-changing work (embedding + graph linking on ingest, scheduling,
/// budget forecasting) always runs locally and for real. Free-form conversation is handled by a
/// Semantic Kernel chat session with the four agent plugins auto-invocable when a model is
/// configured (<see cref="AiOptions.IsChatEnabled"/>); otherwise the same plugins are called
/// directly and their findings narrated, so the SignalR streaming path is exercised offline.
///
/// <para>
/// This is the seam where Semantic Kernel's experimental <c>AgentGroupChat</c> — or its
/// successor, the Microsoft Agent Framework — would be substituted without changing the
/// <see cref="IAgentOrchestrator"/> contract or any caller.
/// </para>
/// </summary>
public sealed class SemanticKernelAgentOrchestrator(
    IServiceScopeFactory scopeFactory,
    IOptions<AiOptions> aiOptions,
    ILogger<SemanticKernelAgentOrchestrator> logger) : IAgentOrchestrator
{
    private const double LinkSimilarityThreshold = 0.75;
    private readonly AiOptions _ai = aiOptions.Value;

    // Built lazily so no client exists unless the Anthropic path runs. When no key is configured,
    // the default client resolves credentials from the environment (ANTHROPIC_API_KEY / profiles).
    private readonly Lazy<AnthropicClient> _claude =
        new(() => string.IsNullOrWhiteSpace(aiOptions.Value.ApiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = aiOptions.Value.ApiKey });

    public async Task<AgentRunResult> RunAsync(AgentCommand command, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var notifier = sp.GetRequiredService<IAgentStreamNotifier>();

        try
        {
            var result = command.Kind switch
            {
                AgentTaskKind.IngestBrainNode => await IngestAsync(sp, notifier, command, cancellationToken),
                AgentTaskKind.AnalyzeBudget => await AnalyzeBudgetAsync(sp, notifier, command, cancellationToken),
                AgentTaskKind.ScheduleTasks => await ScheduleAsync(sp, notifier, command, cancellationToken),
                AgentTaskKind.Conversation => await ConverseAsync(sp, notifier, command, cancellationToken),
                _ => AgentRunResult.Fail($"Unsupported agent task kind '{command.Kind}'.")
            };

            await notifier.CompletedAsync(command.CorrelationId, result, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent run {CorrelationId} ({Kind}) failed", command.CorrelationId, command.Kind);
            var failure = AgentRunResult.Fail(ex.Message);
            await notifier.CompletedAsync(command.CorrelationId, failure, CancellationToken.None);
            return failure;
        }
    }

    // ---- Second Brain: embed the node, then link it into the knowledge graph. ----
    private async Task<AgentRunResult> IngestAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        CancellationToken ct)
    {
        if (command.BrainNodeId is not { } nodeId)
        {
            return AgentRunResult.Fail("Ingestion command did not specify a BrainNodeId.");
        }

        var db = sp.GetRequiredService<IApplicationDbContext>();
        var embeddings = sp.GetRequiredService<IEmbeddingService>();
        var search = sp.GetRequiredService<IBrainNodeSearch>();

        await notifier.AgentActivatedAsync(command.CorrelationId, SecondBrainAgentPlugin.Name, ct);

        var node = await db.BrainNodes.FirstOrDefaultAsync(n => n.Id == nodeId, ct);
        if (node is null)
        {
            return AgentRunResult.Fail($"BrainNode '{nodeId}' no longer exists.");
        }

        await notifier.MilestoneAsync(command.CorrelationId, $"Embedding \"{node.Title}\"…", ct);
        node.Embedding = await embeddings.EmbedAsync($"{node.Title}\n{node.Content}", ct);
        await db.SaveChangesAsync(ct);
        await notifier.MilestoneAsync(command.CorrelationId, "Embedding stored.", ct);

        // Find semantic neighbours and materialize graph edges above the similarity threshold.
        var neighbours = await search.SearchAsync(command.UserId, node.Embedding!, limit: 10, ct);
        var existingTargets = await db.NodeLinks
            .Where(l => l.SourceNodeId == nodeId)
            .Select(l => l.TargetNodeId)
            .ToListAsync(ct);
        var existing = existingTargets.ToHashSet();

        var linked = 0;
        foreach (var match in neighbours)
        {
            if (match.Id == nodeId || match.Similarity < LinkSimilarityThreshold || !existing.Add(match.Id))
            {
                continue;
            }

            db.NodeLinks.Add(new NodeLink
            {
                SourceNodeId = nodeId,
                TargetNodeId = match.Id,
                SimilarityScore = match.Similarity
            });
            linked++;
        }

        if (linked > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var summary = $"Ingested \"{node.Title}\": embedded and linked to {linked} related node(s).";
        await notifier.MilestoneAsync(command.CorrelationId, summary, ct);
        return AgentRunResult.Ok(summary);
    }

    // ---- Finance: forecast spend via the ML.NET service (reused through MediatR). ----
    private async Task<AgentRunResult> AnalyzeBudgetAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        CancellationToken ct)
    {
        var db = sp.GetRequiredService<IApplicationDbContext>();
        var mediator = sp.GetRequiredService<IMediator>();

        await notifier.AgentActivatedAsync(command.CorrelationId, FinanceAgentPlugin.Name, ct);

        var planId = command.BudgetPlanId;
        if (planId is null)
        {
            planId = await db.BudgetPlans
                .Where(p => p.UserId == command.UserId && p.IsActive)
                .OrderByDescending(p => p.StartDate)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (planId is null)
        {
            return AgentRunResult.Fail("No active budget plan to analyze.");
        }

        var forecast = await mediator.Send(new ForecastBudgetQuery(planId.Value, HorizonMonths: 3), ct);

        var sb = new StringBuilder();
        sb.Append(forecast.ModelTrained
            ? $"Trend is {(forecast.MonthlyTrend >= 0 ? "rising" : "falling")} by {Math.Abs(forecast.MonthlyTrend):C}/month. "
            : "Not enough history for a trend; projecting a flat average. ");
        foreach (var p in forecast.Points)
        {
            sb.Append($"{p.Year}-{p.Month:00}: {p.ProjectedSpend:C}. ");
        }

        var summary = sb.ToString().TrimEnd();
        await StreamTextAsync(notifier, command.CorrelationId, FinanceAgentPlugin.Name, summary, ct);
        return AgentRunResult.Ok(summary);
    }

    // ---- Schedule: re-flow unscheduled tasks (reused through MediatR). ----
    private async Task<AgentRunResult> ScheduleAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        CancellationToken ct)
    {
        var mediator = sp.GetRequiredService<IMediator>();
        await notifier.AgentActivatedAsync(command.CorrelationId, ScheduleAgentPlugin.Name, ct);

        var scheduled = await mediator.Send(new AutoScheduleTasksCommand(command.UserId), ct);
        var summary = scheduled.Count == 0
            ? "No unscheduled tasks to place."
            : $"Placed {scheduled.Count} task(s). Next up: {scheduled[0].Title} at {scheduled[0].ScheduledStart:yyyy-MM-dd HH:mm}.";

        await StreamTextAsync(notifier, command.CorrelationId, ScheduleAgentPlugin.Name, summary, ct);
        return AgentRunResult.Ok(summary);
    }

    // ---- Conversation: the full multi-agent group chat. ----
    private async Task<AgentRunResult> ConverseAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        CancellationToken ct)
    {
        var prompt = string.IsNullOrWhiteSpace(command.Prompt)
            ? "Give me a status overview across my schedule, budget, and knowledge base."
            : command.Prompt!;

        if (_ai.IsAnthropic)
        {
            return await ConverseWithClaudeAsync(sp, notifier, command, prompt, ct);
        }

        return _ai.IsChatEnabled
            ? await ConverseWithKernelAsync(sp, notifier, command, prompt, ct)
            : await ConverseOfflineAsync(sp, notifier, command, prompt, ct);
    }

    // ---- Conversation via Anthropic Claude (official SDK), streamed to the client. ----
    private async Task<AgentRunResult> ConverseWithClaudeAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        string prompt,
        CancellationToken ct)
    {
        await notifier.AgentActivatedAsync(command.CorrelationId, "Coordinator", ct);

        // Ground Claude in the user's real data by first querying the specialist agents, then
        // passing their findings as context. (Letting Claude call these as tools directly — a
        // tool-use loop — is the natural next iteration.)
        var (schedule, finance, secondBrain, _) = BuildPlugins(sp, command.UserId);
        var unscheduled = await schedule.CountUnscheduled();
        var upcoming = await schedule.GetUpcomingTasks(5);
        var budget = await finance.SummarizeActiveBudget();
        var related = await secondBrain.SearchKnowledge(prompt, 5);
        var nodeCount = await secondBrain.CountNodes();

        var context = new StringBuilder()
            .AppendLine("## Schedule")
            .AppendLine($"Unscheduled tasks: {unscheduled}")
            .AppendLine(upcoming)
            .AppendLine("## Finance")
            .AppendLine(budget)
            .AppendLine("## Second Brain")
            .AppendLine($"Total nodes: {nodeCount}")
            .AppendLine("Most relevant notes:")
            .AppendLine(related)
            .ToString();

        try
        {
            var parameters = new MessageCreateParams
            {
                Model = _ai.AnthropicModel,
                MaxTokens = 4096,
                System =
                    "You are Chronaiq, a personal-productivity coordinator. Answer using ONLY the " +
                    "user-data context provided in the message. Be concise, specific, and action-oriented; " +
                    "cite concrete tasks, amounts, and notes from the context. If the context lacks the " +
                    "answer, say so rather than inventing details.",
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"User request: {prompt}\n\n--- Live context ---\n{context}"
                    }
                ]
            };

            var full = new StringBuilder();
            await foreach (var streamEvent in _claude.Value.Messages.CreateStreaming(parameters))
            {
                ct.ThrowIfCancellationRequested();
                if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                    delta.Delta.TryPickText(out var text))
                {
                    full.Append(text.Text);
                    await notifier.TokenAsync(command.CorrelationId, "Coordinator", text.Text, ct);
                }
            }

            var answer = full.ToString();
            return AgentRunResult.Ok(string.IsNullOrWhiteSpace(answer) ? "(no content)" : answer);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Resilience: if Claude is unreachable or misconfigured, degrade to the offline
            // narrator instead of failing the whole run.
            logger.LogWarning(ex, "Claude conversation failed; falling back to offline narration.");
            await notifier.MilestoneAsync(
                command.CorrelationId, "Claude unavailable — using offline overview.", ct);
            return await ConverseOfflineAsync(sp, notifier, command, prompt, ct);
        }
    }

    private async Task<AgentRunResult> ConverseWithKernelAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        string prompt,
        CancellationToken ct)
    {
        await notifier.AgentActivatedAsync(command.CorrelationId, "Coordinator", ct);

        var kernel = BuildKernel(sp, command.UserId);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are Chronaiq, a productivity coordinator with Schedule, Finance, Diagram, and " +
            "SecondBrain specialist tools. Use the tools to ground every claim in the user's real " +
            "data. Be concise and actionable.");
        history.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var full = new StringBuilder();
        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                full.Append(chunk.Content);
                await notifier.TokenAsync(command.CorrelationId, "Coordinator", chunk.Content, ct);
            }
        }

        var answer = full.ToString();
        return AgentRunResult.Ok(string.IsNullOrWhiteSpace(answer) ? "(no content)" : answer);
    }

    private async Task<AgentRunResult> ConverseOfflineAsync(
        IServiceProvider sp,
        IAgentStreamNotifier notifier,
        AgentCommand command,
        string prompt,
        CancellationToken ct)
    {
        var (schedule, finance, secondBrain, diagram) = BuildPlugins(sp, command.UserId);

        await notifier.AgentActivatedAsync(command.CorrelationId, ScheduleAgentPlugin.Name, ct);
        var unscheduled = await schedule.CountUnscheduled();
        var upcoming = await schedule.GetUpcomingTasks(3);

        await notifier.AgentActivatedAsync(command.CorrelationId, FinanceAgentPlugin.Name, ct);
        var budget = await finance.SummarizeActiveBudget();

        await notifier.AgentActivatedAsync(command.CorrelationId, SecondBrainAgentPlugin.Name, ct);
        var related = await secondBrain.SearchKnowledge(prompt, 3);
        var nodeCount = await secondBrain.CountNodes();

        var report = new StringBuilder()
            .AppendLine("Chronaiq status overview (offline mode — no chat model configured):")
            .AppendLine()
            .AppendLine($"Schedule: {unscheduled} unscheduled task(s).")
            .AppendLine(upcoming)
            .AppendLine($"Finance: {budget}")
            .AppendLine($"Second Brain: {nodeCount} node(s). Most related to your prompt:")
            .AppendLine(related)
            .ToString();

        await StreamTextAsync(notifier, command.CorrelationId, "Coordinator", report, ct);

        // The Diagram agent's Mermaid output is delivered as a final milestone artifact.
        var mermaid = await diagram.RenderKnowledgeGraph();
        await notifier.MilestoneAsync(command.CorrelationId, mermaid, ct);

        return AgentRunResult.Ok(report.Trim());
    }

    private Kernel BuildKernel(IServiceProvider sp, Guid userId)
    {
        var builder = Kernel.CreateBuilder();

        switch (_ai.Provider)
        {
            case AiProvider.AzureOpenAI:
                builder.AddAzureOpenAIChatCompletion(_ai.ChatModel, _ai.Endpoint!, _ai.ApiKey!);
                break;
            case AiProvider.OpenAI:
            default:
                builder.AddOpenAIChatCompletion(_ai.ChatModel, _ai.ApiKey!);
                break;
        }

        var (schedule, finance, secondBrain, diagram) = BuildPlugins(sp, userId);
        builder.Plugins.AddFromObject(schedule, ScheduleAgentPlugin.Name);
        builder.Plugins.AddFromObject(finance, FinanceAgentPlugin.Name);
        builder.Plugins.AddFromObject(secondBrain, SecondBrainAgentPlugin.Name);
        builder.Plugins.AddFromObject(diagram, DiagramAgentPlugin.Name);

        return builder.Build();
    }

    private static (ScheduleAgentPlugin Schedule, FinanceAgentPlugin Finance,
        SecondBrainAgentPlugin SecondBrain, DiagramAgentPlugin Diagram) BuildPlugins(
        IServiceProvider sp, Guid userId)
    {
        var db = sp.GetRequiredService<IApplicationDbContext>();
        var embeddings = sp.GetRequiredService<IEmbeddingService>();
        var search = sp.GetRequiredService<IBrainNodeSearch>();

        return (
            new ScheduleAgentPlugin(db, userId),
            new FinanceAgentPlugin(db, userId),
            new SecondBrainAgentPlugin(db, embeddings, search, userId),
            new DiagramAgentPlugin(db, userId));
    }

    /// <summary>Streams text back as word-level deltas, emulating an LLM token stream over SignalR.</summary>
    private static async Task StreamTextAsync(
        IAgentStreamNotifier notifier,
        Guid correlationId,
        string agentName,
        string text,
        CancellationToken ct)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            ct.ThrowIfCancellationRequested();
            await notifier.TokenAsync(correlationId, agentName, word + ' ', ct);
        }
    }
}
