using Chronaiq.Domain.Entities;

namespace Chronaiq.Application.Features.Tasks.Models;

/// <summary>Read model for a scheduled (or not-yet-scheduled) task.</summary>
public sealed record CalTaskDto(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    int EnergyRequirement,
    int DurationMinutes,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    bool IsCompleted)
{
    public static CalTaskDto FromEntity(CalTask task) => new(
        task.Id,
        task.UserId,
        task.Title,
        task.Description,
        task.EnergyRequirement,
        task.DurationMinutes,
        task.ScheduledStart,
        task.ScheduledEnd,
        task.IsCompleted);
}
