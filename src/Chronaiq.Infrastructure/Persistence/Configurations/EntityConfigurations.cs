using Chronaiq.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Chronaiq.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent configuration for every aggregate. Table and column shapes mirror the reference
/// PostgreSQL schema exactly (quoted PascalCase identifiers, column types, precision, and the
/// pgvector HNSW index) so an EF migration reproduces that schema faithfully.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);

        // Chronotype persists as its underlying int, matching the schema's INT column.
        builder.Property(u => u.Chronotype).HasConversion<int>().IsRequired();

        builder.Property(u => u.PreferredWorkStart).HasColumnType("time").IsRequired();
        builder.Property(u => u.PreferredWorkEnd).HasColumnType("time").IsRequired();

        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
    }
}

public sealed class BrainNodeConfiguration : IEntityTypeConfiguration<BrainNode>
{
    public void Configure(EntityTypeBuilder<BrainNode> builder)
    {
        builder.ToTable("BrainNodes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(255).IsRequired();
        builder.Property(n => n.Content).IsRequired();
        builder.Property(n => n.SourceType).HasMaxLength(50).IsRequired();
        builder.Property(n => n.RawFileUrl).HasMaxLength(512);

        // The domain carries the embedding as float[]; here it is mapped to the pgvector
        // vector(1536) column. EF applies this converter only to non-null values and stores
        // NULL for pending (not-yet-embedded) nodes.
        builder.Property(n => n.Embedding)
            .HasColumnType("vector(1536)")
            .HasConversion(
                v => new Vector(v!),
                v => v.ToArray());

        // Approximate-nearest-neighbour index over cosine distance, matching the schema.
        builder.HasIndex(n => n.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder.HasOne(n => n.User)
            .WithMany(u => u.BrainNodes)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NodeLinkConfiguration : IEntityTypeConfiguration<NodeLink>
{
    public void Configure(EntityTypeBuilder<NodeLink> builder)
    {
        builder.ToTable("NodeLinks");
        builder.HasKey(l => new { l.SourceNodeId, l.TargetNodeId });

        builder.Property(l => l.SimilarityScore).IsRequired();

        builder.HasOne(l => l.SourceNode)
            .WithMany(n => n.OutgoingLinks)
            .HasForeignKey(l => l.SourceNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // The reverse edge also cascades in the schema. PostgreSQL permits the two cascade
        // paths; Restrict here would contradict the reference DDL.
        builder.HasOne(l => l.TargetNode)
            .WithMany(n => n.IncomingLinks)
            .HasForeignKey(l => l.TargetNodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CalTaskConfiguration : IEntityTypeConfiguration<CalTask>
{
    public void Configure(EntityTypeBuilder<CalTask> builder)
    {
        builder.ToTable("CalTasks", t =>
            t.HasCheckConstraint("CK_CalTasks_EnergyRequirement", "\"EnergyRequirement\" BETWEEN 1 AND 5"));

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(255).IsRequired();
        builder.Property(t => t.Description);
        builder.Property(t => t.EnergyRequirement).IsRequired();
        builder.Property(t => t.DurationMinutes).IsRequired();

        builder.HasOne(t => t.User)
            .WithMany(u => u.Tasks)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
{
    public void Configure(EntityTypeBuilder<BudgetPlan> builder)
    {
        builder.ToTable("BudgetPlans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.MonthlyIncome).HasPrecision(15, 2);
        builder.Property(p => p.MonthlySavingsTarget).HasPrecision(15, 2);
        builder.Property(p => p.StartDate).HasColumnType("date").IsRequired();
        builder.Property(p => p.EndDate).HasColumnType("date");

        builder.HasOne(p => p.User)
            .WithMany(u => u.BudgetPlans)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Amount).HasPrecision(15, 2).IsRequired();
        builder.Property(t => t.MerchantName).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Category).HasMaxLength(100).IsRequired();
        builder.Property(t => t.TransactionDate).IsRequired();

        builder.HasOne(t => t.BudgetPlan)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TaskReferenceConfiguration : IEntityTypeConfiguration<TaskReference>
{
    public void Configure(EntityTypeBuilder<TaskReference> builder)
    {
        builder.ToTable("TaskReferences");
        builder.HasKey(r => new { r.TaskId, r.BrainNodeId });

        builder.HasOne(r => r.Task)
            .WithMany(t => t.References)
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.BrainNode)
            .WithMany(n => n.TaskReferences)
            .HasForeignKey(r => r.BrainNodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
