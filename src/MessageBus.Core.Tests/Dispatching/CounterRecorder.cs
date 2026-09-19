using System.Diagnostics.Metrics;

namespace MessageBus.Core.Tests.Dispatching;

/// <summary>
/// Collects one instrument out of the endpoint's meter. Listening to the real meter rather than
/// substituting one is the point: a counter nobody emits to would pass a mock and fail a dashboard.
/// </summary>
internal sealed class CounterRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<string> _endpointTags = [];

    private long _total;

    public CounterRecorder(IMeterFactory meterFactory, string instrumentName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Scope == meterFactory && instrument.Name == instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            Interlocked.Add(ref _total, measurement);

            foreach (var tag in tags)
            {
                if (tag.Key == "messaging.endpoint" && tag.Value is string endpoint)
                {
                    lock (_endpointTags)
                    {
                        _endpointTags.Add(endpoint);
                    }
                }
            }
        });

        _listener.Start();
    }

    public long Total
        => Interlocked.Read(ref _total);

    public IReadOnlyList<string> EndpointTags
    {
        get
        {
            lock (_endpointTags)
            {
                return [.. _endpointTags];
            }
        }
    }

    public void Dispose()
        => _listener.Dispose();
}
