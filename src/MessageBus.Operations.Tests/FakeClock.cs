namespace MessageBus.Operations.Tests;

/// <summary>A clock a test can move, so staleness does not need a real wait.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow()
        => Now;
}
