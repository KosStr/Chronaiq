using Chronaiq.Application.Common.Persistence;
using Chronaiq.Application.Features.Users.Models;
using Chronaiq.Domain.Entities;
using Chronaiq.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Chronaiq.Application.Features.Users;

/// <summary>Registers a new user profile.</summary>
public sealed record CreateUserCommand(
    string Email,
    string? FirstName,
    string? LastName,
    Chronotype Chronotype,
    TimeOnly? PreferredWorkStart,
    TimeOnly? PreferredWorkEnd) : IRequest<UserDto>;

public sealed class CreateUserHandler(IApplicationDbContext db)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"A user with email '{email}' already exists.");
        }

        var user = new User
        {
            Email = email,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            Chronotype = request.Chronotype,
            PreferredWorkStart = request.PreferredWorkStart ?? new TimeOnly(9, 0),
            PreferredWorkEnd = request.PreferredWorkEnd ?? new TimeOnly(17, 0)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return UserDto.FromEntity(user);
    }
}
