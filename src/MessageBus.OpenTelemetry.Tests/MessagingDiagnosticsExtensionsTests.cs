using System.Diagnostics;
using System.Diagnostics.Metrics;
using MessageBus.Core.Dispatching;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MessageBus.OpenTelemetry.Tests;

public sealed class MessagingDiagnosticsExtensionsTests
{
    [Fact]
    public void AddMessaging_OnTracerProviderBuilder_ShouldMakeTheMessagingActivitySourceHaveListeners()
    {
        // Arrange
        using var activitySource = new ActivitySource(MessagingDiagnostics.ActivitySourceName);

        // Act
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddMessaging()
            .Build();

        // Assert
        Assert.True(activitySource.HasListeners());
    }

    [Fact]
    public void AddMessaging_OnMeterProviderBuilder_ShouldPublishMetricsFromTheMessagingMeter()
    {
        // Arrange
        var exportedMetrics = new List<Metric>();
        using var meter = new Meter(MessagingDiagnostics.MeterName);
        var counter = meter.CreateCounter<long>("test-counter");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMessaging()
            .AddInMemoryExporter(exportedMetrics)
            .Build();

        // Act
        counter.Add(1);
        provider.ForceFlush();

        // Assert
        Assert.Contains(exportedMetrics, metric => metric.MeterName == MessagingDiagnostics.MeterName);
    }
}
