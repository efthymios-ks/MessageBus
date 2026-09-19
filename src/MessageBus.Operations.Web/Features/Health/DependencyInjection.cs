using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MessageBus.Operations.Web.Features.Health;

/// <summary>Wires up the readiness and liveness endpoints for the Operations host.</summary>
public static class DependencyInjection
{
    private const string LivenessTag = "live";

    /// <summary>Registers the health check services.</summary>
    public static WebApplicationBuilder AddOperationsHealth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LivenessTag]);

        return builder;
    }

    /// <summary>Maps the health check endpoints: <c>/health</c> for readiness, <c>/alive</c> for liveness.</summary>
    public static WebApplication MapOperationsHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LivenessTag)
        });

        return app;
    }
}
