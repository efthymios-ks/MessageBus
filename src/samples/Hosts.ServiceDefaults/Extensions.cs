using MessageBus.OpenTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MessageBus.Hosts.ServiceDefaults;

/// <summary>
/// Telemetry, health checks and service discovery shared by every sample endpoint.
/// </summary>
public static class Extensions
{
    private const string LivenessTag = "live";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureOpenTelemetry();

        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LivenessTag]);

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var seqUrl = builder.Configuration.GetConnectionString("Seq");
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            var seqOtlpEndpoint = new Uri(new Uri(seqUrl), "ingest/otlp/v1/logs");

            builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter(exporter =>
            {
                exporter.Endpoint = seqOtlpEndpoint;
                exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
            }));
        }

        // OTEL_EXPORTER_OTLP_ENDPOINT is what Aspire injects; when set, metrics and traces flow to
        // the Aspire dashboard. Logs go to the dashboard via stdout capture, so no log exporter is
        // registered here — mixing multiple log OTLP exporters trips the SDK's registration check.
        var aspireOtlpConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        var openTelemetry = builder.Services.AddOpenTelemetry();

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMessaging();

            if (aspireOtlpConfigured)
            {
                metrics.AddOtlpExporter();
            }
        });

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMessaging();

            if (aspireOtlpConfigured)
            {
                tracing.AddOtlpExporter();
            }
        });

        return builder;
    }

    /// <summary>
    /// <c>/health</c> answers for readiness, <c>/alive</c> for liveness. Development only —
    /// in production these belong behind an internal port.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new()
        {
            Predicate = healthCheck => healthCheck.Tags.Contains(LivenessTag)
        });

        return app;
    }
}
