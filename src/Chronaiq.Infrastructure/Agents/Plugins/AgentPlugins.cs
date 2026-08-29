using System.ComponentModel;
using System.Text;
using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Chronaiq.Infrastructure.Agents.Plugins;

/// <summary>
/// The "Second Brain" specialist. Exposes knowledge-base lookups both to the LLM (as
/// auto-invocable <see cref="KernelFunctionAttribute"/>s during an AgentGroupChat) and to the
/// orchestrator directly for the offline path. Every function is scoped to a single user.
/// </summary>
public sealed class SecondBrainAgentPlugin(
    IApplicationDbContext db,
    IEmbeddingService embeddings,
    IBrainNodeSearch search,
    Guid userId)
{
    public const string Name = "SecondBrain";

    [KernelFunction, Description("Semantically searches the user's knowledge base and returns the most relevant notes.")]
    public async Task<string> SearchKnowledge(
        [Description("What to search for.")] string query,
        [Description("Maximum number of notes to return.")] int limit = 5)
    {
        var embedding = await embeddings.EmbedAsync(query);
        var matches = await search.SearchAsync(userId, embedding, Math.Clamp(limit, 1, 20));
        if (matches.Count == 0)
        {
            return "No related notes were found in the Second Brain.";
        }

        var sb = new StringBuilder();
        foreach (var m in matches)
        {
            sb.AppendLine($"- {m.Title} ({m.SourceType}), similarity {m.Similarity:0.00}");
        }

        return sb.ToString();
    }

    [KernelFunction, Description("Counts how many knowledge nodes the user has ingested.")]
    public Task<int> CountNodes()
        => db.BrainNodes.CountAsync(n => n.UserId == userId);
}

/// <summary>The Schedule specialist: reports on the user's calendar and workload.</summary>
public sealed class ScheduleAgentPlugin(IApplicationDbContext db, Guid userId)
{
    public const string Name = "Schedule";

    [KernelFunction, Description("Lists the user's next scheduled tasks in chronological order.")]
    public async Task<string> GetUpcomingTasks([Description("Maximum tasks to list.")] int max = 10)
    {
        var now = DateTimeOffset.UtcNow;
        var tasks = await db.Tasks
            .Where(t => t.UserId == userId && !t.IsCompleted && t.ScheduledStart >= now)
            .OrderBy(t => t.ScheduledStart)
            .Take(Math.Clamp(max, 1, 50))
            .Select(t => new { t.Title, t.ScheduledStart, t.EnergyRequirement })
            .ToListAsync();

        if (tasks.Count == 0)
        {
            return "There are no upcoming scheduled tasks.";
        }

        var sb = new StringBuilder();
        foreach (var t in tasks)
        {
            sb.AppendLine($"- {t.ScheduledStart:yyyy-MM-dd HH:mm} — {t.Title} (energy {t.EnergyRequirement}/5)");
        }

        return sb.ToString();
    }

    [KernelFunction, Description("Counts tasks that are not yet placed on the calendar.")]
    public Task<int> CountUnscheduled()
        => db.Tasks.CountAsync(t => t.UserId == userId && !t.IsCompleted && t.ScheduledStart == null);
}

/// <summary>The Finance specialist: summarizes the active budget and its ledger.</summary>
public sealed class FinanceAgentPlugin(IApplicationDbContext db, Guid userId)
{
    public const string Name = "Finance";

    [KernelFunction, Description("Summarizes the user's active budget plan, including income, savings target, and total spend.")]
    public async Task<string> SummarizeActiveBudget()
    {
        var plan = await db.BudgetPlans
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync();

        if (plan is null)
        {
            return "The user has no active budget plan.";
        }

        var totalSpend = await db.Transactions
            .Where(t => t.BudgetPlanId == plan.Id)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return $"Budget '{plan.Name}': income {plan.MonthlyIncome:C}, savings target " +
               $"{plan.MonthlySavingsTarget:C}, recorded spend {totalSpend:C}.";
    }
}

/// <summary>
/// The Diagram specialist: renders the user's knowledge graph as Mermaid so it can be shown in
/// the client without any external rendering service.
/// </summary>
public sealed class DiagramAgentPlugin(IApplicationDbContext db, Guid userId)
{
    public const string Name = "Diagram";

    [KernelFunction, Description("Renders the user's knowledge graph (nodes and their links) as a Mermaid flowchart.")]
    public async Task<string> RenderKnowledgeGraph([Description("Maximum nodes to include.")] int maxNodes = 25)
    {
        var nodes = await db.BrainNodes
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(maxNodes, 1, 100))
            .Select(n => new { n.Id, n.Title })
            .ToListAsync();

        if (nodes.Count == 0)
        {
            return "flowchart LR\n  empty[No knowledge nodes yet]";
        }

        var ids = nodes.Select(n => n.Id).ToHashSet();
        var links = await db.NodeLinks
            .Where(l => ids.Contains(l.SourceNodeId) && ids.Contains(l.TargetNodeId))
            .Select(l => new { l.SourceNodeId, l.TargetNodeId, l.SimilarityScore })
            .ToListAsync();

        var alias = nodes
            .Select((n, i) => (n.Id, Alias: $"n{i}", n.Title))
            .ToDictionary(x => x.Id, x => (x.Alias, x.Title));

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        foreach (var (_, value) in alias)
        {
            sb.AppendLine($"  {value.Alias}[\"{Escape(value.Title)}\"]");
        }

        foreach (var link in links)
        {
            var from = alias[link.SourceNodeId].Alias;
            var to = alias[link.TargetNodeId].Alias;
            sb.AppendLine($"  {from} -->|{link.SimilarityScore:0.00}| {to}");
        }

        return sb.ToString();
    }

    private static string Escape(string title)
        => title.Replace("\"", "'").Replace("\n", " ");
}
