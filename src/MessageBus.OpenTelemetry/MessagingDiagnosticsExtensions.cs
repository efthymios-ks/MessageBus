using MessageBus.Core.Dispatching;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MessageBus.OpenTelemetry;

/// <summary>Wires the messaging meter and activity source into OpenTelemetry.</summary>
public static class MessagingDiagnosticsExtensions
{
    /// <summary>Registers the messaging meter so its instruments are exported.</summary>
    public static MeterProviderBuilder AddMessaging(this MeterProviderBuilder builder)
        => builder.AddMeter(MessagingDiagnostics.MeterName);

    /// <summary>Registers the messaging activity source so its spans are exported.</summary>
    public static TracerProviderBuilder AddMessaging(this TracerProviderBuilder builder)
        => builder.AddSource(MessagingDiagnostics.ActivitySourceName);
}
