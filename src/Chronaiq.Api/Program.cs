using System.Text.Json.Serialization;
using Chronaiq.Api.Common;
using Chronaiq.Api.Endpoints;
using Chronaiq.Api.Hubs;
using Chronaiq.Api.Observability;
using Chronaiq.Api.Realtime;
using Chronaiq.Api.Workers;
using Chronaiq.Application;
using Chronaiq.Application.Common.Agents;
using Chronaiq.Infrastructure;
using Serilog;

// A bootstrap logger captures anything that happens before the host — and its configuration-driven
// logger — is ready, so failures during startup are never lost. It is swapped for the fully
// configured logger once the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Chronaiq API");
    RunApp(args);
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Chronaiq API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static void RunApp(string[] args)
{
var builder = WebApplication.CreateBuilder(args);

// Serilog replaces the default logging providers and reads its sinks/levels/enrichers from the
// "Serilog" configuration section; ReadFrom.Services lets DI-registered enrichers participate.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---- Clean Architecture composition: Application + Infrastructure. ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The SignalR-backed notifier is the API layer's implementation of the Application contract.
builder.Services.AddScoped<IAgentStreamNotifier, SignalRAgentStreamNotifier>();

// The consumer half of the in-memory command pipeline.
builder.Services.AddHostedService<AgentExecutionWorker>();

// ---- Cross-cutting: real-time, telemetry, problem details, OpenAPI. ----
builder.Services.AddSignalR();
builder.Services.AddChronaiqTelemetry(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

// Outermost middleware: emits one structured completion log per request (method, path, status,
// elapsed) after the exception handler has settled the final response.
app.UseSerilogRequestLogging();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ---- Endpoints. ----
app.MapUserEndpoints();
app.MapBrainEndpoints();
app.MapTaskEndpoints();
app.MapBudgetEndpoints();
app.MapAgentEndpoints();

app.MapHub<AgentHub>("/hubs/agent");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).WithTags("System");

app.Run();
}

/// <summary>Exposed so integration tests can reference the API host via WebApplicationFactory.</summary>
public partial class Program;
