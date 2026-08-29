using Chronaiq.Application.Common.AI;
using Chronaiq.Application.Common.Agents;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Common.Storage;
using Chronaiq.Infrastructure.AI;
using Chronaiq.Infrastructure.Agents;
using Chronaiq.Infrastructure.Analytics;
using Chronaiq.Infrastructure.Messaging;
using Chronaiq.Infrastructure.Persistence;
using Chronaiq.Infrastructure.Persistence.Interceptors;
using Chronaiq.Infrastructure.Persistence.Repositories;
using Chronaiq.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Chronaiq.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: PostgreSQL/pgvector persistence, the
/// in-memory command pipeline, the AI services, and the ML.NET forecaster. The API layer calls
/// this and additionally supplies the SignalR-backed <see cref="IAgentStreamNotifier"/>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        AddPersistence(services, configuration);

        // In-memory producer/consumer pipeline shared by the API and the hosted worker.
        services.AddSingleton<IAgentCommandQueue, ChannelAgentCommandQueue>();

        // AI + analytics services. Deterministic/offline-capable by default; see AiOptions.
        services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
        services.AddSingleton<IBudgetForecastService, MlNetBudgetForecastService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // The orchestrator creates its own scope per command, so it is safe as a singleton.
        services.AddSingleton<IAgentOrchestrator, SemanticKernelAgentOrchestrator>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' was not configured (ConnectionStrings:Postgres).");

        // A single pgvector-enabled data source is shared by EF Core and the raw ADO similarity
        // queries, so the vector type mapping is registered exactly once.
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<ChronaiqDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    sp.GetRequiredService<NpgsqlDataSource>(),
                    npgsql => npgsql.UseVector())
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ChronaiqDbContext>());
        services.AddScoped<IBrainNodeSearch, BrainNodeSearch>();
    }
}
