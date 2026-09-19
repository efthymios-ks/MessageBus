using MessageBus.Core.Configuration;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Tests.Pipeline;

public sealed class MessageResolutionBehaviorTests
{
    [Fact]
    public async Task ProcessAsync_WhenTheWireNameIsUnknown_DeadLettersTheMessage()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        harness.Broker.Redeliver(Message("Somebody.Elses.Message.v1", "{}"u8.ToArray()));

        await MessagingTestHarness.WaitForAsync(() => harness.Broker.DeadLetteredMessages.Count == 1);

        // Assert
        Assert.Contains("No type is mapped", harness.Broker.DeadLetteredMessages[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_WhenTheTypeIsKnownButUnhandled_MovesTheMessageToTheErrorQueue()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        harness.Broker.Redeliver(Message(typeof(AttributedMessage).FullName!, "{}"u8.ToArray()));

        await MessagingTestHarness.WaitForAsync(() => harness.Broker.QueueDepth("messagebus-error") == 1);

        // Assert
        Assert.Empty(harness.Broker.DeadLetteredMessages);
    }

    [Fact]
    public async Task ProcessAsync_WhenThePayloadCannotBeRead_DeadLettersTheMessage()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        harness.Broker.Redeliver(Message(typeof(PlaceOrder).FullName!, "not json"u8.ToArray()));

        await MessagingTestHarness.WaitForAsync(() => harness.Broker.DeadLetteredMessages.Count == 1);

        // Assert
        Assert.Contains("could not be read", harness.Broker.DeadLetteredMessages[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_WhenAMessageIsDeadLettered_DoesNotReachTheErrorQueue()
    {
        // Arrange
        var log = new MessageLog();
        await using var harness = await StartAsync(log);

        // Act
        harness.Broker.Redeliver(Message("Somebody.Elses.Message.v1", "{}"u8.ToArray()));

        await MessagingTestHarness.WaitForAsync(() => harness.Broker.DeadLetteredMessages.Count == 1);

        // Assert
        Assert.Equal(0, harness.Broker.QueueDepth("messagebus-error"));
    }

    private static TransportMessage Message(string messageTypeName, byte[] payload)
        => new()
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageTypeName = messageTypeName,
            Payload = payload,
            Destination = "test-endpoint",
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessageHeaders.MessageIntent] = MessageHeaders.CommandIntent
            }
        };

    private static Task<MessagingTestHarness> StartAsync(MessageLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler))
                .WithDefaultRetryPolicy(RetryPolicy.None),
            services => services.AddSingleton(log)
        );
}
