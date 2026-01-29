using KeryxFlux.Application.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace KeryxFlux.Application;

/// <summary>
/// Extension methods for registering Application layer services
/// </summary>
public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Register all Application layer services (MediatR, handlers, etc.)
    /// </summary>
    public static IServiceCollection AddKeryxFluxApplication(this IServiceCollection services)
    {
        // Register MediatR with handlers from this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceRegistration).Assembly);
        });

        return services;
    }
}
