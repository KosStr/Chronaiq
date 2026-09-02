using Chronaiq.Application.Features.Users;
using Chronaiq.Domain.Enums;
using MediatR;

namespace Chronaiq.Api.Endpoints;

/// <summary>User profile endpoints.</summary>
public static class UserEndpoints
{
    public sealed record CreateUserRequest(
        string Email,
        string? FirstName,
        string? LastName,
        Chronotype Chronotype,
        TimeOnly? PreferredWorkStart,
        TimeOnly? PreferredWorkEnd);

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapPost("/", async (CreateUserRequest request, ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(new CreateUserCommand(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Chronotype,
                request.PreferredWorkStart,
                request.PreferredWorkEnd), ct);

            return Results.Created($"/api/users/{user.Id}", user);
        })
        .WithName("CreateUser");

        group.MapGet("/{userId:guid}", async (Guid userId, ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(new GetUserByIdQuery(userId), ct);
            return Results.Ok(user);
        })
        .WithName("GetUserById");

        return app;
    }
}
