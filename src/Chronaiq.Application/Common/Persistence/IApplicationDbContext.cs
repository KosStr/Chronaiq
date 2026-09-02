using Chronaiq.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Common.Persistence;

/// <summary>
/// Abstraction over the EF Core context, exposed to the Application layer so that command
/// and query handlers depend on a contract rather than on the concrete Infrastructure
/// <c>ChronaiqDbContext</c>. Vector-similarity search is deliberately excluded here — it
/// requires provider-specific SQL and lives behind <see cref="IBrainNodeSearch"/>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<BrainNode> BrainNodes { get; }
    DbSet<NodeLink> NodeLinks { get; }
    DbSet<CalTask> Tasks { get; }
    DbSet<BudgetPlan> BudgetPlans { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TaskReference> TaskReferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
