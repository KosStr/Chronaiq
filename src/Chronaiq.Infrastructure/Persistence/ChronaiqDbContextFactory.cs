using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chronaiq.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef migrations add</c>,
/// <c>dotnet ef database update</c>). It does not need a live database — only a provider and a
/// connection string shape — so migrations can be generated offline. At runtime the context is
/// configured by the API's DI instead (see Infrastructure <c>DependencyInjection</c>).
/// </summary>
public sealed class ChronaiqDbContextFactory : IDesignTimeDbContextFactory<ChronaiqDbContext>
{
    public ChronaiqDbContext CreateDbContext(string[] args)
    {
        // A design-time connection string. Overridable via env var so the same tooling can point
        // at a real database when applying migrations.
        var connectionString =
            Environment.GetEnvironmentVariable("CHRONAIQ_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=chronaiq;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ChronaiqDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsAssembly(typeof(ChronaiqDbContextFactory).Assembly.FullName);
            })
            .Options;

        return new ChronaiqDbContext(options);
    }
}
