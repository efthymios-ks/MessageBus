using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MessageBus.Operations.Web.Features.OpenTelemetry;

/// <summary>Wires up logging, metrics and tracing for the Operations host.</summary>
public static class DependencyInjection
{
    /// <summary>Registers OpenTelemetry logging, metrics and tracing, with Seq and Aspire exporters when configured.</summary>
    public static WebApplicationBuilder AddOperationsTelemetry(this WebApplicationBuilder builder)
    {
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
                .AddRuntimeInstrumentation();

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
                .AddHttpClientInstrumentation();

            if (aspireOtlpConfigured)
            {
                tracing.AddOtlpExporter();
            }
        });

        return builder;
    }
}
