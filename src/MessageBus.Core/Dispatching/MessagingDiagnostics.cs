namespace MessageBus.Core.Dispatching;

/// <summary>
/// The names an application registers with OpenTelemetry. Constants rather than literals, because
/// a mistyped source name shows up as silence rather than as an error.
/// </summary>
public static class MessagingDiagnostics
{
    /// <summary>Name to register the <see cref="System.Diagnostics.ActivitySource"/> under.</summary>
    public const string ActivitySourceName = "MessageBus";

    /// <summary>Name to register the <see cref="System.Diagnostics.Metrics.Meter"/> under.</summary>
    public const string MeterName = "MessageBus";
}
