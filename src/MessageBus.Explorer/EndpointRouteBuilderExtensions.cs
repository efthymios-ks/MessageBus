using MessageBus.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Mime;

namespace MessageBus.Explorer;

/// <summary>Maps the message explorer page onto an <see cref="IEndpointRouteBuilder"/>.</summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a page listing every message this endpoint handles, each with an editable payload and a
    /// send button. It dispatches real messages into the real pipeline, so whether it is mapped at
    /// all is a decision the host makes explicitly — a predicate over configuration rather than an
    /// environment name, because "which environments may send test messages" is a deployment
    /// question and environment names are only one way of answering it.
    /// </summary>
    public static IEndpointRouteBuilder MapMessageExplorer(
        this IEndpointRouteBuilder endpoints,
        Func<IConfiguration, bool> isEnabled,
        string routePrefix = "/messages"
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(isEnabled);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        var services = endpoints.ServiceProvider;

        if (!isEnabled(services.GetRequiredService<IConfiguration>()))
        {
            return endpoints;
        }

        var endpointName = services.GetRequiredService<MessagingOptions>().EndpointName;

        endpoints.MapGet(routePrefix, (HttpContext httpContext) =>
        {
            var explorer = Explorer(httpContext);

            return Html(MessageExplorerPage.Render(
                endpointName,
                routePrefix,
                explorer.GetMessages(),
                httpContext.Request.Query["sent"],
                httpContext.Request.Query["error"],
                httpContext.Request.Query["wire"],
                httpContext.Request.Query["body"]
            ));
        });

        endpoints.MapPost(routePrefix, async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var wireName = form["wireName"].ToString();
            var body = form["body"].ToString();

            try
            {
                await Explorer(httpContext).SendAsync(wireName, body, cancellationToken);

                var sentAt = httpContext.RequestServices.GetRequiredService<TimeProvider>()
                    .GetUtcNow()
                    .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

                // Post, redirect, get: a refresh after sending should not send again, least of all
                // on a page whose entire purpose is sending. The body is echoed back so the textarea
                // keeps the value the operator just sent instead of resetting to a fresh sample.
                return Results.Redirect(
                    $"{routePrefix}"
                        + $"?sent={Uri.EscapeDataString($"Sent {wireName} at {sentAt}.")}"
                        + $"&wire={Uri.EscapeDataString(wireName)}"
                        + $"&body={Uri.EscapeDataString(body)}"
                );
            }
            catch (Exception exception)
            {
                return Results.Redirect(
                    $"{routePrefix}"
                        + $"?error={Uri.EscapeDataString(exception.Message)}"
                        + $"&wire={Uri.EscapeDataString(wireName)}"
                        + $"&body={Uri.EscapeDataString(body)}"
                );
            }
        });

        return endpoints;
    }

    /// <summary>
    /// Resolved per request rather than injected: the explorer reaches the scoped dispatcher and
    /// persistence, which are the same ones a controller in this host would get.
    /// </summary>
    private static MessageExplorerService Explorer(HttpContext httpContext)
        => ActivatorUtilities.CreateInstance<MessageExplorerService>(httpContext.RequestServices);

    private static IResult Html(string page)
        => Results.Content(page, $"{MediaTypeNames.Text.Html}; charset=utf-8");
}
