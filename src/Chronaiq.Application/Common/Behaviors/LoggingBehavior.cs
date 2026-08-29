using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronaiq.Application.Common.Behaviors;

/// <summary>
/// Cross-cutting MediatR behavior that logs the start, completion, and duration of every
/// request. Registered open-generically so it wraps all commands and queries. Timing is
/// captured with <see cref="Stopwatch.GetTimestamp"/> to avoid allocating a Stopwatch per
/// request on the hot path.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var start = Stopwatch.GetTimestamp();

        logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next(cancellationToken);
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs} ms",
                requestName,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{RequestName} failed after {ElapsedMs} ms",
                requestName,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }
}
