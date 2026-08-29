using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Tasks.Models;
using Chronaiq.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Tasks;

/// <summary>Creates an unscheduled task. Auto-scheduling is a separate, explicit step.</summary>
public sealed record CreateCalTaskCommand(
    Guid UserId,
    string Title,
    string? Description,
    int EnergyRequirement,
    int DurationMinutes) : IRequest<CalTaskDto>;

public sealed class CreateCalTaskHandler(IApplicationDbContext db)
    : IRequestHandler<CreateCalTaskCommand, CalTaskDto>
{
    public async Task<CalTaskDto> Handle(CreateCalTaskCommand request, CancellationToken cancellationToken)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException(nameof(User), request.UserId);
        }

        var task = new CalTask
        {
            UserId = request.UserId,
            Title = request.Title.Trim(),
            Description = request.Description,
            EnergyRequirement = Math.Clamp(request.EnergyRequirement, 1, 5),
            DurationMinutes = Math.Max(5, request.DurationMinutes)
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);

        return CalTaskDto.FromEntity(task);
    }
}
