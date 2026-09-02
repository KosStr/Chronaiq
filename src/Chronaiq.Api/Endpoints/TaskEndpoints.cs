using Chronaiq.Application.Features.Tasks;
using MediatR;

namespace Chronaiq.Api.Endpoints;

/// <summary>Task creation and chronotype-aware auto-scheduling endpoints.</summary>
public static class TaskEndpoints
{
    public sealed record CreateTaskRequest(
        string Title,
        string? Description,
        int EnergyRequirement,
        int DurationMinutes);

    public sealed record AutoScheduleRequest(DateOnly? FromDate, bool Reschedule = false);

    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/{userId:guid}/tasks").WithTags("Tasks");

        group.MapPost("/", async (Guid userId, CreateTaskRequest request, ISender sender, CancellationToken ct) =>
        {
            var task = await sender.Send(new CreateCalTaskCommand(
                userId,
                request.Title,
                request.Description,
                request.EnergyRequirement,
                request.DurationMinutes), ct);

            return Results.Created($"/api/users/{userId}/tasks/{task.Id}", task);
        })
        .WithName("CreateTask");

        // Synchronous scheduling. The same work can be triggered asynchronously through the
        // agent pipeline (see AgentEndpoints) when the caller wants live progress over SignalR.
        group.MapPost("/auto-schedule", async (
            Guid userId,
            AutoScheduleRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var scheduled = await sender.Send(new AutoScheduleTasksCommand(
                userId,
                request?.FromDate,
                request?.Reschedule ?? false), ct);

            return Results.Ok(scheduled);
        })
        .WithName("AutoScheduleTasks");

        return app;
    }
}
