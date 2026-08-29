using Chronaiq.Application.Features.BrainNodes;
using Chronaiq.Domain.Enums;
using MediatR;

namespace Chronaiq.Api.Endpoints;

/// <summary>Second Brain ingestion and semantic search endpoints.</summary>
public static class BrainEndpoints
{
    public static IEndpointRouteBuilder MapBrainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users/{userId:guid}/brain").WithTags("Second Brain");

        // Multipart ingest: persists the (optional) file, creates a pending node, enqueues the
        // embed+link work, and returns 202 with the correlation id to subscribe to over SignalR.
        group.MapPost("/ingest", async (
            Guid userId,
            HttpRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Expected multipart/form-data with Title, Content, and SourceType fields.");
            }

            var form = await request.ReadFormAsync(ct);
            var title = form["Title"].ToString();
            var content = form["Content"].ToString();
            var sourceType = string.IsNullOrWhiteSpace(form["SourceType"]) ? BrainSourceType.Document : form["SourceType"].ToString();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                return Results.BadRequest("Title and Content are required.");
            }

            var file = form.Files.GetFile("File");
            await using var stream = file?.OpenReadStream() ?? Stream.Null;

            var accepted = await sender.Send(new IngestBrainNodeCommand(
                userId,
                title,
                content,
                sourceType,
                file is null ? null : stream,
                file?.FileName), ct);

            return Results.Accepted($"/api/users/{userId}/brain/{accepted.BrainNodeId}", accepted);
        })
        .WithName("IngestBrainNode")
        .DisableAntiforgery();

        group.MapGet("/search", async (
            Guid userId,
            string q,
            int? limit,
            ISender sender,
            CancellationToken ct) =>
        {
            var matches = await sender.Send(new SearchBrainNodesQuery(userId, q, limit ?? 10), ct);
            return Results.Ok(matches);
        })
        .WithName("SearchBrainNodes");

        return app;
    }
}
