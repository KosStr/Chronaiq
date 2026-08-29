using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Tasks.Models;
using Chronaiq.Application.Features.Tasks.Scheduling;
using Chronaiq.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Tasks;

/// <summary>
/// Re-flows a user's unscheduled, incomplete tasks across the calendar using their
/// chronotype and preferred working window. Idempotent for already-scheduled tasks: by
/// default only tasks without a <see cref="CalTask.ScheduledStart"/> are considered, unless
/// <see cref="Reschedule"/> is set, in which case all incomplete tasks are re-planned.
/// </summary>
public sealed record AutoScheduleTasksCommand(
    Guid UserId,
    DateOnly? FromDate = null,
    bool Reschedule = false) : IRequest<IReadOnlyList<CalTaskDto>>;

public sealed class AutoScheduleTasksHandler(IApplicationDbContext db)
    : IRequestHandler<AutoScheduleTasksCommand, IReadOnlyList<CalTaskDto>>
{
    public async Task<IReadOnlyList<CalTaskDto>> Handle(
        AutoScheduleTasksCommand request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var candidates = await db.Tasks
            .Where(t => t.UserId == request.UserId
                        && !t.IsCompleted
                        && (request.Reschedule || t.ScheduledStart == null))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        var firstDay = request.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var planned = ChronotypeScheduler.Plan(
            candidates,
            user.Chronotype,
            user.PreferredWorkStart,
            user.PreferredWorkEnd,
            firstDay);

        await db.SaveChangesAsync(cancellationToken);

        return planned
            .OrderBy(t => t.ScheduledStart)
            .Select(CalTaskDto.FromEntity)
            .ToList();
    }
}
