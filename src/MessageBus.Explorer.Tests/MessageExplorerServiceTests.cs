using MessageBus.Core.Configuration;
using MessageBus.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Explorer.Tests;

/// <summary>
/// Against a running endpoint, because the explorer's whole claim is that it sends the way the
/// application does — a substituted dispatcher would prove nothing about that.
/// </summary>
public sealed class MessageExplorerServiceTests
{
    [Fact]
    public async Task GetMessages_WhenTheEndpointHandlesSomething_ListsItByWireName()
    {
        // Arrange
        await using var harness = await StartAsync(new HandlerLog());

        // Act
        var messages = Explorer(harness).GetMessages();

        // Assert
        Assert.Contains(messages, message => message.WireName.EndsWith(nameof(PlaceOrder), StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetMessages_WhenAMessageIsListed_NamesTheHandlersThatWouldRun()
    {
        // Arrange
        await using var harness = await StartAsync(new HandlerLog());

        // Act
        var message = Explorer(harness).GetMessages().First(candidate =>
            candidate.WireName.EndsWith(nameof(PlaceOrder), StringComparison.Ordinal));

        // Assert
        Assert.Contains(typeof(PlaceOrderHandler).FullName, message.HandlerNames);
    }

    [Fact]
    public async Task GetMessages_WhenAMessageIsAnEvent_SaysSo()
    {
        // Arrange
        await using var harness = await StartAsync(new HandlerLog());

        // Act
        var message = Explorer(harness).GetMessages().First(candidate =>
            candidate.WireName.EndsWith(nameof(OrderPlaced), StringComparison.Ordinal));

        // Assert
        Assert.Equal("Event", message.Kind);
    }

    [Fact]
    public async Task SendAsync_WhenTheSampleIsSentUnedited_ReachesTheHandler()
    {
        // Arrange
        var log = new HandlerLog();
        await using var harness = await StartAsync(log);

        var explorer = Explorer(harness);
        var message = explorer.GetMessages().First(candidate =>
            candidate.WireName.EndsWith(nameof(PlaceOrder), StringComparison.Ordinal));

        // Act
        await explorer.SendAsync(message.WireName, message.SampleJson, CancellationToken.None);
        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Single(log.Entries, entry => entry.StartsWith("handled:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_WhenTheBodyIsEdited_TheHandlerSeesTheEditedValues()
    {
        // Arrange
        var log = new HandlerLog();
        await using var harness = await StartAsync(log);

        var explorer = Explorer(harness);
        var message = explorer.GetMessages().First(candidate =>
            candidate.WireName.EndsWith(nameof(PlaceOrder), StringComparison.Ordinal));

        // Act
        await explorer.SendAsync(
            message.WireName,
            message.SampleJson.Replace("\"orderId\": \"\"", "\"orderId\": \"order-7\""),
            CancellationToken.None
        );

        await harness.WaitUntilQuietAsync();

        // Assert
        Assert.Contains("handled:order-7", log.Entries);
    }

    [Fact]
    public async Task SendAsync_WhenTheWireNameIsUnknown_Throws()
    {
        // Arrange
        await using var harness = await StartAsync(new HandlerLog());

        var explorer = Explorer(harness);

        // Act
        Task Act()
            => explorer.SendAsync("Nobody.Knows.This.v1", "{}", CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(Act);
    }

    private static MessageExplorerService Explorer(MessagingTestHarness harness)
        => ActivatorUtilities.CreateInstance<MessageExplorerService>(harness.Services.CreateScope().ServiceProvider);

    private static Task<MessagingTestHarness> StartAsync(HandlerLog log)
        => MessagingTestHarness.StartAsync(
            "test-endpoint",
            messaging => messaging
                .WithFullNameMessageTypeResolver<PlaceOrder>()
                .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
                .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderPlacedHandler)),
            services => services.AddSingleton(log)
        );
}
