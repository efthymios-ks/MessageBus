namespace MessageBus.Core.TypeResolution;

/// <summary>
/// CLR type to the name on the wire, and back. Asymmetric on purpose.
/// </summary>
public interface IMessageTypeResolver
{
    /// <summary>Throws for an unregistered type — an outgoing message nobody can name is a bug here.</summary>
    string GetMessageTypeName(Type messageType);

    /// <summary>
    /// Null for an unknown name, which is someone else's deployment rather than a bug here. The
    /// dispatcher moves the message to the error queue.
    /// </summary>
    Type? GetMessageType(string messageTypeName);

    /// <summary>
    /// Every type this resolver can name. Startup validation needs the set to report missing routes
    /// all at once, which it cannot do by asking type by type.
    /// </summary>
    IReadOnlyCollection<Type> KnownMessageTypes { get; }
}
