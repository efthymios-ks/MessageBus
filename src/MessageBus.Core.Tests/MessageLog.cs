namespace MessageBus.Core.Tests;

/// <summary>
/// What the handlers under test write to. A shared sink rather than a substitute: the handlers are
/// resolved by the pipeline from its own scope, so there is nothing for a test to hand them.
/// </summary>
public sealed class MessageLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public int FailuresRemaining { get; set; }

    /// <summary>Long enough not to fire, except in the test that is about firing.</summary>
    public TimeSpan SagaTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public void Add(string entry)
    {
        lock (_entries)
        {
            _entries.Add(entry);
        }
    }
}
