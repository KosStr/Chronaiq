using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Chronaiq.Api.Observability;

/// <summary>
/// Wires OpenTelemetry traces and metrics for the API. Mirrors the .NET Aspire ServiceDefaults
/// shape (resource attributes + ASP.NET Core/HTTP/runtime instrumentation) and exports over OTLP
/// only when an endpoint is configured, so running the app standalone produces no exporter noise.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>ActivitySource the orchestrator/worker can emit custom spans on.</summary>
    public const string ActivitySourceName = "Chronaiq";

    public static IServiceCollection AddChronaiqTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "Chronaiq.Api",
                serviceVersion: typeof(TelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddSource("Npgsql") // Npgsql emits its own spans for DB round-trips.
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            otel.UseOtlpExporter();
        }

        return services;
    }
}
