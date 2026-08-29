using Chronaiq.Application.Common.Exceptions;
using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Users.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Users;

/// <summary>Fetches a single user profile by id.</summary>
public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserDto>;

public sealed class GetUserByIdHandler(IApplicationDbContext db)
    : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.UserId);

        return UserDto.FromEntity(user);
    }
}
