namespace MessageBus.Core.Pipeline;

/// <summary>
/// Two values, not three. A reply is a command; that it answers something is visible from its
/// causation id, not from its intent.
/// </summary>
public enum MessageIntent
{
    /// <summary>Addressed to one endpoint's queue.</summary>
    Command,

    /// <summary>Published to a topic. Every subscriber gets a copy.</summary>
    Event
}
