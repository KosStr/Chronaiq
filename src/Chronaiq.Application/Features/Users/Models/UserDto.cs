using Chronaiq.Domain.Entities;
using Chronaiq.Domain.Enums;

namespace Chronaiq.Application.Features.Users.Models;

/// <summary>Read model for a user profile.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    Chronotype Chronotype,
    TimeOnly PreferredWorkStart,
    TimeOnly PreferredWorkEnd,
    DateTimeOffset CreatedAt)
{
    public static UserDto FromEntity(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Chronotype,
        user.PreferredWorkStart,
        user.PreferredWorkEnd,
        user.CreatedAt);
}
