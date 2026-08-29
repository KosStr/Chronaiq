# Chronaiq

An event-driven, multi-agent personal-productivity backend built on **.NET 10** with Clean
Architecture. A single API host accepts commands, persists state to **PostgreSQL + pgvector**,
enqueues long-running work onto an in-memory pipeline, and streams agent progress back to clients
over **SignalR**.

> **Framework note.** The original brief titled this ".NET 10" but listed ".NET 9 / EF Core 9" in
> the stack table. The installed SDK and the scaffolded project both target **net10.0**, so the
> solution is built on **.NET 10 / EF Core 10**. See *Deviations from the brief* below.

## Solution layout (Clean Architecture)

```
Chronaiq.slnx
src/
  Chronaiq.Domain          — entities, enums, invariants. No dependencies.
  Chronaiq.Application      — MediatR commands/queries, DTOs, and the abstractions
                             (IApplicationDbContext, IAgentOrchestrator, IEmbeddingService,
                             IBudgetForecastService, IAgentCommandQueue, IFileStorage, …).
  Chronaiq.Infrastructure   — EF Core + pgvector, ML.NET forecaster, Semantic Kernel
                             orchestration, channel queue, local file storage.
  Chronaiq.Api             — Minimal API host: endpoints, SignalR hub, the background worker,
                             OpenTelemetry, and DI composition.
```

Dependencies point inward only: `Api → Infrastructure → Application → Domain`.

## Request → agent → client flow

1. A client `POST`s a command (e.g. brain ingest, or `/agents/converse`).
2. The endpoint persists state, **enqueues an `AgentCommand`** onto a bounded
   `System.Threading.Channels` pipeline, and returns **`202 Accepted`** with a `correlationId`.
3. `AgentExecutionWorker` (a `BackgroundService`) drains the channel and hands each command to
   `IAgentOrchestrator` inside its **own DI scope**.
4. The orchestrator runs the specialist agents (Schedule, Finance, Diagram, Second Brain), which
   query the database and do real work (embedding, graph linking, ML.NET forecasting, scheduling).
5. Progress — agent activations, token deltas, milestones, and the final result — is pushed to the
   client's **SignalR group** (`/hubs/agent`, joined via `Subscribe(correlationId)`).

## Prerequisites

- .NET SDK 10.x
- PostgreSQL 14+ with the `vector` (pgvector) and `uuid-ossp` extensions available
- (optional) An OpenAI/Azure OpenAI key to enable the live chat path — see *AI configuration*

## Getting started

```bash
# 1. Point the API at your database
#    src/Chronaiq.Api/appsettings.json → ConnectionStrings:Postgres

# 2. Apply the schema (creates extensions, tables, the vector column, and the HNSW index)
dotnet dotnet-ef database update -p src/Chronaiq.Infrastructure -s src/Chronaiq.Api

# 3. Run the API
dotnet run --project src/Chronaiq.Api
```

Then open the OpenAPI document at `/openapi/v1.json`, or drive the API with
`src/Chronaiq.Api/Chronaiq.Api.http`.

## AI configuration

The system **runs fully offline by default** (`Ai:Provider = None`):

- **Embeddings** are produced by a deterministic, L2-normalized bag-of-tokens hash into the
  1536-dim space, so the entire ingest → embed → HNSW search → graph-link pipeline works without
  any external model or key. Swap in a real model (`text-embedding-3-small`) by replacing the body
  of `DeterministicEmbeddingService.EmbedAsync`.
- **Conversation** falls back to a data-grounded narrated report. Configure a chat provider to
  upgrade the coordinator to a real LLM:
  - `Ai:Provider = Anthropic` + `Ai:ApiKey` (model via `Ai:AnthropicModel`, default `claude-opus-5`)
    uses **Claude** through the official Anthropic .NET SDK. The coordinator first queries the four
    specialist agents for the user's real data, then **streams** Claude's grounded answer to the
    client over SignalR. If Claude is unreachable, it degrades to the offline narrator. The API key
    can also come from the standard `ANTHROPIC_API_KEY` environment variable (leave `Ai:ApiKey`
    blank and set `Ai:Provider = Anthropic`).
  - `Ai:Provider = OpenAI` / `AzureOpenAI` + `Ai:ApiKey` (and `Ai:Endpoint` for Azure) uses the
    **Semantic Kernel** chat path, where the four agent plugins become auto-invocable tools.

## What's fully implemented

- Domain model + EF Core configuration mapping the reference schema **exactly** (quoted PascalCase
  identifiers, `vector(1536)`, the `CalTasks` energy check constraint, composite keys, cascade
  behavior) — verified by the generated `InitialCreate` migration.
- pgvector cosine-distance (`<=>`) search over the HNSW index via a shared, vector-enabled
  `NpgsqlDataSource`.
- The 202 command pipeline (bounded channel producer/consumer) and the hosted worker.
- A real, deterministic **chronotype-aware scheduler** (`ChronotypeScheduler`) — pure and testable.
- A real **ML.NET** SDCA regression forecaster with a graceful flat fallback for thin history.
- SignalR hub + notifier streaming the full agent lifecycle.
- OpenTelemetry traces/metrics (ASP.NET Core, HTTP, runtime, Npgsql), OTLP export when an endpoint
  is configured — the `.NET Aspire` ServiceDefaults shape.

## Deviations from the brief (and why)

- **.NET 10, not 9** — matches the installed SDK and the scaffolded `net10.0` project.
- **Agent orchestration is behind `IAgentOrchestrator`, implemented with stable Semantic Kernel
  chat + function-calling**, rather than SK's experimental `AgentGroupChat`. That type is being
  superseded by the Microsoft Agent Framework; the interface is the seam to drop either in without
  touching callers.
- **MediatR** is now under a commercial license (v14). Fine for a prototype; note it before
  production use.
- The worker runs **in-process** with the API (the pipeline is in-memory by design). Splitting it
  into a separate host would require a durable transport (e.g. Postgres queue / Redis) in place of
  `System.Threading.Channels`.

## Tech stack

.NET 10 · ASP.NET Core Minimal APIs · MediatR · EF Core 10 · Npgsql + pgvector · Semantic Kernel ·
ML.NET · SignalR · Serilog · OpenTelemetry
