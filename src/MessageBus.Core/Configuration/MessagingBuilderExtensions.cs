using System.Reflection;
using System.Text.Json;
using MessageBus.Core.Routing;
using MessageBus.Core.Serialization;
using MessageBus.Core.TypeResolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Core.Configuration;

/// <summary>
/// The sugar over the generic builder methods. A package ships its own — <c>WithRabbitMqTransport</c>
/// — the same way, which is what makes a third-party provider indistinguishable from a first-party
/// one.
/// </summary>
public static class MessagingBuilderExtensions
{
    /// <summary>Registers the JSON serializer with default options.</summary>
    public static IMessagingBuilder WithJsonSerializer(this IMessagingBuilder builder)
        => builder.WithJsonSerializer(_ => { });

    /// <summary>Registers the JSON serializer and lets the caller adjust its options.</summary>
    public static IMessagingBuilder WithJsonSerializer(
        this IMessagingBuilder builder,
        Action<JsonSerializerOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var serializerOptions = JsonMessageSerializer.CreateDefaultOptions();
        configure(serializerOptions);

        builder.Services.Replace(ServiceDescriptor.Singleton<IMessageSerializer>(
            new JsonMessageSerializer(serializerOptions)
        ));

        return builder;
    }

    /// <summary>Names every message type by namespace and class name.</summary>
    public static IMessagingBuilder WithFullNameMessageTypeResolver<TMessageMarker>(this IMessagingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Replace(ServiceDescriptor.Singleton<IMessageTypeResolver>(
            new FullNameMessageTypeResolver([typeof(TMessageMarker).Assembly])
        ));

        return builder;
    }

    /// <summary>Uses the attribute-driven router so each message points at its destination in code.</summary>
    public static IMessagingBuilder WithAttributeMessageRouting(this IMessagingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMessageRouter<AttributeMessageRouter>();
    }

    /// <summary>
    /// Scans the assembly holding <typeparamref name="THandlerMarker"/> for handlers.
    /// Marker-type overload survives refactoring — moving handlers to another project becomes a
    /// compiler error rather than an empty scan.
    /// </summary>
    public static IMessagingBuilder WithMessageHandlersFromAssemblyOf<THandlerMarker>(
        this IMessagingBuilder builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithMessageHandlersFromAssembly(typeof(THandlerMarker).Assembly);
    }

    /// <summary>Scans the entry assembly for handlers. Fails when there is no entry assembly to scan.</summary>
    public static IMessagingBuilder WithMessageHandlersFromEntryAssembly(this IMessagingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Null under some test runners, which deserves saying so rather than registering nothing.
        var entryAssembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException(
                "There is no entry assembly to scan. Name the assembly instead, with "
                    + $"{nameof(WithMessageHandlersFromAssemblyOf)}."
            );

        return builder.WithMessageHandlersFromAssembly(entryAssembly);
    }
}
