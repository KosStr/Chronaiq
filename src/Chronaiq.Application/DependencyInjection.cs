using System.Reflection;
using Chronaiq.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Chronaiq.Application;

/// <summary>
/// Composition root for the Application layer. Registers MediatR (scanning this assembly for
/// all command/query handlers) and the cross-cutting pipeline behaviors. Everything here is
/// pure application logic — no Infrastructure concerns.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Behaviors run in registration order, outermost first.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
