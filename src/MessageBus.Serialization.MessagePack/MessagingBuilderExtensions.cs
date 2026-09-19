using MessageBus.Core.Configuration;
using MessageBus.Core.Serialization;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Serialization.MessagePack;

/// <summary>Registers the MessagePack serializer on an <see cref="IMessagingBuilder"/>.</summary>
public static class MessagingBuilderExtensions
{
    /// <summary>
    /// Replaces the JSON serializer. Both ends of a message have to agree: the content type travels
    /// in a header so a consumer can see what it was given, but nothing translates between formats.
    /// </summary>
    public static IMessagingBuilder WithMessagePackSerializer(this IMessagingBuilder builder)
        => builder.WithMessagePackSerializer(options => options);

    /// <summary>Replaces the JSON serializer with MessagePack, applying <paramref name="configure"/> to the defaults.</summary>
    public static IMessagingBuilder WithMessagePackSerializer(
        this IMessagingBuilder builder,
        Func<MessagePackSerializerOptions, MessagePackSerializerOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var serializerOptions = configure(MessagePackMessageSerializer.CreateDefaultOptions());

        builder.Services.Replace(ServiceDescriptor.Singleton<IMessageSerializer>(
            new MessagePackMessageSerializer(serializerOptions)
        ));

        return builder;
    }
}
