using MessageBus.Core.Configuration;

namespace MessageBus.Core.TypeResolution;

/// <summary>
/// Stands in until a resolver is chosen. There is no safe default — naming messages by CLR type is
/// a decision with deployment consequences — so the placeholder says what to do rather than picking
/// for the endpoint.
/// </summary>
internal sealed class UnconfiguredMessageTypeResolver : IMessageTypeResolver
{
    public IReadOnlyCollection<Type> KnownMessageTypes
        => [];

    public string GetMessageTypeName(Type messageType)
        => throw NotConfigured();

    public Type? GetMessageType(string messageTypeName)
        => throw NotConfigured();

    private static InvalidOperationException NotConfigured()
        => new(
            $"No {nameof(IMessageTypeResolver)} is configured. Add "
                + $"{nameof(IMessagingBuilder.WithMessageTypeMap)} to declare wire names, or "
                + $"{nameof(MessagingBuilderExtensions.WithFullNameMessageTypeResolver)} to derive "
                + "them from CLR names."
        );
}
