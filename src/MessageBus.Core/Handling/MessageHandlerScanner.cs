using System.Reflection;
using MessageBus.Abstractions.Handling;

namespace MessageBus.Core.Handling;

/// <summary>
/// Finds handlers and sagas in the assemblies given, one descriptor per
/// <see cref="IMessageHandler{TMessage}"/> a type closes — a saga implements it several times,
/// which is the normal case.
/// </summary>
internal static class MessageHandlerScanner
{
    public static IReadOnlyList<MessageHandlerDescriptor> Scan(IReadOnlyList<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return Describe(assemblies.SelectMany(assembly => assembly.GetTypes()));
    }

    public static IReadOnlyList<MessageHandlerDescriptor> Describe(IEnumerable<Type> candidateTypes)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);

        var descriptors = new List<MessageHandlerDescriptor>();

        var handlerTypes = candidateTypes
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var handlerType in handlerTypes)
        {
            var sagaStateType = SagaStateTypeOf(handlerType);

            foreach (var handledMessageType in HandledMessageTypes(handlerType))
            {
                descriptors.Add(new MessageHandlerDescriptor(
                    HandlerType: handlerType,
                    MessageType: handledMessageType,
                    IsSagaStarter: IsSagaStarter(handlerType, handledMessageType),
                    SagaType: sagaStateType is null ? null : handlerType,
                    SagaStateType: sagaStateType
                ));
            }
        }

        return descriptors;
    }

    private static IEnumerable<Type> HandledMessageTypes(Type handlerType)
        => handlerType
            .GetInterfaces()
            .Where(candidate
                => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IMessageHandler<>)
            )
            .Select(candidate => candidate.GetGenericArguments()[0])
            .Distinct();

    /// <summary>
    /// Matched on the message type rather than the interface: <see cref="ISagaStarter{TMessage}"/>
    /// derives from <see cref="IMessageHandler{TMessage}"/>, so both show up in
    /// <see cref="Type.GetInterfaces"/> and matching on identity puts the flag on the wrong message.
    /// </summary>
    private static bool IsSagaStarter(Type handlerType, Type messageType)
        => handlerType
            .GetInterfaces()
            .Any(candidate
                => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(ISagaStarter<>)
                && candidate.GetGenericArguments()[0] == messageType
            );

    private static Type? SagaStateTypeOf(Type handlerType)
    {
        for (var candidate = handlerType.BaseType; candidate is not null; candidate = candidate.BaseType)
        {
            if (candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(Saga<>)
            )
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
