using System.Collections.Concurrent;
using System.Reflection;
using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Handling;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Persistence;
using MessageBus.Core.TypeResolution;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Handling;

/// <summary>
/// Calls one handler. A saga is the same call with its state loaded before and written after, both
/// inside the transaction the pipeline already opened.
/// </summary>
internal sealed class MessageHandlerInvoker(IServiceProvider services, IMessagingPersistence persistence)
{
    private static readonly ConcurrentDictionary<Type, Func<object, IMessage, IMessageContext, Task>> _invokers = new();

    private static readonly MethodInfo _invokeTypedMethod
        = typeof(MessageHandlerInvoker).GetMethod(nameof(InvokeTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public async Task InvokeAsync(
        MessageHandlerDescriptor descriptor,
        IMessage message,
        IMessageContext messageContext,
        CancellationToken cancellationToken
    )
    {
        var handler = services.GetRequiredService(descriptor.HandlerType);

        if (handler is not Saga saga)
        {
            await InvokerFor(descriptor.MessageType)(handler, message, messageContext);

            return;
        }

        var correlationId = saga.CorrelateMessage(message);
        var sagaTypeName = SagaTypeNameOf(descriptor.HandlerType);
        var existing = await persistence.Sagas.FindAsync(sagaTypeName, correlationId, cancellationToken);

        if (existing is null && !descriptor.IsSagaStarter)
        {
            // Not an error: a message for a saga that has completed, or one that arrived before its
            // starter. Dropping it is what stops a late timeout from resurrecting a finished saga.
            return;
        }

        saga.StateObject = existing is null
            ? NewState(saga.StateType, correlationId)
            : SagaStateSerializer.Deserialize(existing.State, saga.StateType);

        await InvokerFor(descriptor.MessageType)(handler, message, messageContext);

        var state = (SagaState)saga.StateObject;

        if (saga.IsCompleted)
        {
            if (existing is not null)
            {
                await persistence.Sagas.DeleteAsync(state.SagaId, cancellationToken);
            }

            return;
        }

        await persistence.Sagas.SaveAsync(
            new SagaRecord(
                SagaId: state.SagaId,
                SagaTypeName: sagaTypeName,
                CorrelationId: correlationId,
                State: SagaStateSerializer.Serialize(state),
                Version: existing?.Version ?? []
            ),
            cancellationToken
        );
    }

    private static SagaState NewState(Type stateType, string correlationId)
    {
        var state = (SagaState)Activator.CreateInstance(stateType)!;

        state.SagaId = Guid.NewGuid();
        state.CorrelationId = correlationId;

        return state;
    }

    private static string SagaTypeNameOf(Type sagaType)
        => sagaType.GetCustomAttribute<SagaNameAttribute>()?.Name ?? TypeNaming.NameOf(sagaType);

    /// <summary>
    /// Cached per message type and closed over the interface, so dispatch costs a delegate call
    /// rather than a <see cref="MethodBase.Invoke(object, object[])"/> on every message.
    /// </summary>
    private static Func<object, IMessage, IMessageContext, Task> InvokerFor(Type messageType)
        => _invokers.GetOrAdd(
            messageType,
            type => _invokeTypedMethod
                .MakeGenericMethod(type)
                .CreateDelegate<Func<object, IMessage, IMessageContext, Task>>()
        );

    private static Task InvokeTypedAsync<TMessage>(object handler, IMessage message, IMessageContext messageContext)
        where TMessage : IMessage
        => ((IMessageHandler<TMessage>)handler).HandleAsync((TMessage)message, messageContext);
}
