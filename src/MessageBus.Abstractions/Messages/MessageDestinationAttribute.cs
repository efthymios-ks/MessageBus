namespace MessageBus.Abstractions.Messages;

/// <summary>
/// The endpoint a command is sent to. Read only when attribute routing is registered, and beaten
/// by an explicit route.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageDestinationAttribute : Attribute
{
    /// <summary>Declares the endpoint a command is sent to.</summary>
    /// <param name="destination">Endpoint queue name.</param>
    public MessageDestinationAttribute(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        Destination = destination;
    }

    /// <summary>Endpoint queue name.</summary>
    public string Destination { get; }
}
