using MessageBus.Operations.Heartbeats;

namespace MessageBus.Operations.Web.Features.Messaging;

internal static class HeartbeatsEndpoint
{
    private const string ApiKeyHeader = "X-Operations-Key";

    public static WebApplication MapHeartbeatsEndpoint(this WebApplication app)
    {
        app.MapPost("/api/heartbeats", async (
            EndpointHeartbeat heartbeat,
            HttpContext httpContext,
            EndpointRegistry registry,
            CancellationToken cancellationToken
        ) =>
        {
            if (!httpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKey))
            {
                return Results.Unauthorized();
            }

            // The key decides which endpoint this is; the name in the body only has to agree with it.
            // Identity taken from a payload field is identity anyone who can reach the API can claim.
            var acknowledgement = await registry.RecordAsync(apiKey!, heartbeat, cancellationToken);

            return acknowledgement is null ? Results.Unauthorized() : Results.Ok(acknowledgement);
        });

        return app;
    }
}
