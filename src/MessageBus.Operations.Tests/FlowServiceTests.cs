using MessageBus.Operations.Flows;

namespace MessageBus.Operations.Tests;

/// <summary>
/// Tree building is pure, so these run without a database. The query behind it is covered by the
/// storage tests; what matters here is the shape it produces.
/// </summary>
public sealed class FlowServiceTests
{
    [Fact]
    public void Build_WhenAMessageCausedAnother_NestsTheChild()
    {
        // Arrange
        var nodes = new[] { Node("m1"), Node("m2", causationId: "m1") };

        // Act
        var flow = FlowService.Build("flow-1", nodes);

        // Assert
        Assert.Equal("m2", Assert.Single(Assert.Single(flow.Roots).Children).Node.MessageId);
    }

    [Fact]
    public void Build_WhenAMessageHasNoCausation_BecomesARoot()
    {
        // Arrange
        var nodes = new[] { Node("m1"), Node("m2") };

        // Act
        var flow = FlowService.Build("flow-1", nodes);

        // Assert
        Assert.Equal(2, flow.Roots.Count);
    }

    [Fact]
    public void Build_WhenACauseWasNeverCollected_StillShowsTheMessage()
    {
        // Arrange
        var nodes = new[] { Node("m2", causationId: "never-collected") };

        // Act
        var flow = FlowService.Build("flow-1", nodes);

        // Assert
        Assert.Equal("m2", Assert.Single(flow.Roots).Node.MessageId);
    }

    [Fact]
    public void Build_WhenACauseWasNeverCollected_SaysTheFlowIsPartial()
    {
        // Arrange
        var nodes = new[] { Node("m2", causationId: "never-collected") };

        // Act
        var flow = FlowService.Build("flow-1", nodes);

        // Assert
        Assert.True(flow.IsPartial);
    }

    [Fact]
    public void Build_WhenEverythingIsPresent_DoesNotClaimToBePartial()
    {
        // Arrange
        var nodes = new[] { Node("m1"), Node("m2", causationId: "m1") };

        // Act
        var flow = FlowService.Build("flow-1", nodes);

        // Assert
        Assert.False(flow.IsPartial);
    }

    [Fact]
    public void Build_WhenTheTreeIsNested_RecordsDepthForRendering()
    {
        // Arrange
        var nodes = new[] { Node("m1"), Node("m2", causationId: "m1"), Node("m3", causationId: "m2") };

        // Act
        var depths = FlowService.Build("flow-1", nodes).Flatten().Select(branch => branch.Depth).ToList();

        // Assert
        Assert.Equal([0, 1, 2], depths);
    }

    [Fact]
    public void Build_WhenSiblingsShareACause_OrdersThemByTime()
    {
        // Arrange
        var root = Node("m1", at: DateTimeOffset.UnixEpoch);
        var later = Node("later", causationId: "m1", at: DateTimeOffset.UnixEpoch.AddMinutes(2));
        var sooner = Node("sooner", causationId: "m1", at: DateTimeOffset.UnixEpoch.AddMinutes(1));

        // Act
        var flow = FlowService.Build("flow-1", [root, later, sooner]);

        // Assert
        Assert.Equal("sooner", Assert.Single(flow.Roots).Children[0].Node.MessageId);
    }

    private static FlowNode Node(string messageId, string? causationId = null, DateTimeOffset? at = null)
        => new(
            messageId,
            causationId,
            "orders-service",
            SentBy: "orders-service",
            "Orders.PlaceOrder.v1",
            at ?? DateTimeOffset.UnixEpoch,
            DurationMilliseconds: 5,
            IsFailed: false,
            FailureSummary: null
        );
}
