using MessageBus.Operations.Flows;

namespace MessageBus.Operations.Tests;

/// <summary>
/// Layout is pure, so it is tested rather than eyeballed. A diagram whose geometry lives in a
/// template is a diagram nobody can assert on.
/// </summary>
public sealed class SequenceDiagramTests
{
    [Fact]
    public void Build_WhenTwoEndpointsTalk_GivesEachALane()
    {
        // Arrange
        var flow = FlowService.Build("flow-1", [Node("m1", sentBy: "orders-service", handledBy: "billing-service")]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.Equal(["orders-service", "billing-service"], diagram.Lanes.Select(lane => lane.EndpointName));
    }

    [Fact]
    public void Build_WhenAnEndpointAppearsTwice_ReusesItsLane()
    {
        // Arrange
        var flow = FlowService.Build("flow-1",
        [
            Node("m1", sentBy: "orders-service", handledBy: "billing-service"),
            Node("m2", sentBy: "billing-service", handledBy: "orders-service", causationId: "m1")
        ]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.Equal(2, diagram.Lanes.Count);
    }

    [Fact]
    public void Build_WhenLanesAreOrdered_FollowsFirstAppearanceRatherThanTheAlphabet()
    {
        // Arrange
        var flow = FlowService.Build("flow-1", [Node("m1", sentBy: "zeta-service", handledBy: "alpha-service")]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.Equal("zeta-service", diagram.Lanes[0].EndpointName);
    }

    [Fact]
    public void Build_WhenAMessageWasSentByNobodyKnown_DrawsItAsASelfCall()
    {
        // Arrange
        var flow = FlowService.Build("flow-1", [Node("m1", sentBy: null, handledBy: "orders-service")]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.True(Assert.Single(diagram.Steps).IsSelfCall);
    }

    [Fact]
    public void Build_WhenAnEndpointSendsToItself_DrawsASelfCall()
    {
        // Arrange
        var flow = FlowService.Build("flow-1", [Node("m1", sentBy: "orders-service", handledBy: "orders-service")]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.True(Assert.Single(diagram.Steps).IsSelfCall);
    }

    [Fact]
    public void Build_WhenStepsAreLaidOut_KeepsCausalOrderDownThePage()
    {
        // Arrange
        var flow = FlowService.Build("flow-1",
        [
            Node("m1", sentBy: "orders-service", handledBy: "orders-service"),
            Node("m2", sentBy: "orders-service", handledBy: "billing-service", causationId: "m1")
        ]);

        // Act
        var steps = SequenceDiagram.Build(flow).Steps;

        // Assert
        Assert.True(steps[0].Y < steps[1].Y);
    }

    [Fact]
    public void Build_WhenAnArrowGoesBackwards_SaysSo()
    {
        // Arrange
        var flow = FlowService.Build("flow-1",
        [
            Node("m1", sentBy: "orders-service", handledBy: "billing-service"),
            Node("m2", sentBy: "billing-service", handledBy: "orders-service", causationId: "m1")
        ]);

        // Act
        var steps = SequenceDiagram.Build(flow).Steps;

        // Assert
        Assert.False(steps[1].PointsRight);
    }

    [Fact]
    public void Build_WhenTheDiagramIsSized_LeavesRoomForEveryStep()
    {
        // Arrange
        var flow = FlowService.Build("flow-1",
        [
            Node("m1", sentBy: "orders-service", handledBy: "billing-service"),
            Node("m2", sentBy: "billing-service", handledBy: "orders-service", causationId: "m1")
        ]);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.True(diagram.Height > diagram.Steps[^1].Y);
    }

    [Fact]
    public void Build_WhenTheFlowIsEmpty_StillProducesADrawableCanvas()
    {
        // Arrange
        var flow = FlowService.Build("flow-1", []);

        // Act
        var diagram = SequenceDiagram.Build(flow);

        // Assert
        Assert.True(diagram is { Width: > 0, Height: > 0 });
    }

    private static FlowNode Node(
        string messageId,
        string? sentBy,
        string handledBy,
        string? causationId = null
    ) => new(
        messageId,
        causationId,
        handledBy,
        sentBy,
        "Orders.PlaceOrder.v1",
        DateTimeOffset.UnixEpoch.AddSeconds(messageId.Length),
        DurationMilliseconds: 5,
        IsFailed: false,
        FailureSummary: null,
        Headers: "{}",
        Body: "{}"
    );
}
