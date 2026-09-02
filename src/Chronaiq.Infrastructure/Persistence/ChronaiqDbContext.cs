using Chronaiq.Application.Common.Persistence;
using Chronaiq.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work for Chronaiq, implementing the Application-facing
/// <see cref="IApplicationDbContext"/> contract. Entity configuration lives in the
/// <c>Configurations</c> folder and is applied by convention; the pgvector extension is
/// declared here so it is created by migrations.
/// </summary>
public sealed class ChronaiqDbContext(DbContextOptions<ChronaiqDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BrainNode> BrainNodes => Set<BrainNode>();
    public DbSet<NodeLink> NodeLinks => Set<NodeLink>();
    public DbSet<CalTask> Tasks => Set<CalTask>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TaskReference> TaskReferences => Set<TaskReference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Extensions the schema depends on. pgvector backs the embedding column and its HNSW
        // index; uuid-ossp is declared for parity with the reference schema even though ids are
        // generated client-side as UUIDv7.
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChronaiqDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
