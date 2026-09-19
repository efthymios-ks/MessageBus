using System.Text;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Transport;
using MessageBus.Operations.Actions;
using MessageBus.Operations.Storage;
using MessageBus.Testing.Transport;

namespace MessageBus.Operations.Tests;

[Collection(OperationsCollection.Name)]
public sealed class FailureActionServiceTests(OperationsFixture fixture)
{
    [Fact]
    public async Task RetryAsync_WhenAFailureIsRetried_RepublishesItToTheEndpointThatFailedIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service");
        var actions = ActionsFor(dbContext, broker);

        // Act
        await actions.RetryAsync(["m1"], "operator", CancellationToken.None);

        // Assert
        Assert.Equal("orders-service", broker.SentMessages.Single().Destination);
    }

    [Fact]
    public async Task RetryAsync_WhenAFailureIsRetried_RestoresTheOriginalMessageId()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service");

        // Act
        await ActionsFor(dbContext, broker).RetryAsync(["m1"], "operator", CancellationToken.None);

        // Assert
        Assert.Equal("m1", broker.SentMessages.Single().Headers[MessageHeaders.MessageId]);
    }

    [Fact]
    public async Task RetryAsync_WhenAFailureIsRetried_DropsTheExceptionHeaders()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        var failure = FailureQueryTests.Failure("m1");
        failure.Headers = "{\"exception-type\":\"System.Exception\",\"correlation-id\":\"flow-1\"}";

        await FailureQueryTests.SeedAsync(dbContext, failure);

        var broker = Broker("orders-service");

        // Act
        await ActionsFor(dbContext, broker).RetryAsync(["m1"], "operator", CancellationToken.None);

        // Assert
        Assert.DoesNotContain(MessageHeaders.ExceptionType, broker.SentMessages.Single().Headers.Keys);
    }

    [Fact]
    public async Task RetryAsync_WhenABatchIsRetried_MarksThemAllAndRecordsWhoDidIt()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();

        await FailureQueryTests.SeedAsync(
            dbContext,
            FailureQueryTests.Failure("m1"),
            FailureQueryTests.Failure("m2")
        );

        var broker = Broker("orders-service");

        // Act
        await ActionsFor(dbContext, broker).RetryAsync(["m1", "m2"], "operator", CancellationToken.None);

        var actions = await dbContext
            .Actions
            .ToArrayAsync();

        // Assert
        Assert.Equal(2, actions.Count(action => action.Actor == "operator" && action.Kind == MessageActionKind.Retry));
    }

    [Fact]
    public async Task ReturnToSourceAsync_WhenADestinationIsGiven_SendsItThereInstead()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service", "shipping-service");

        // Act
        await ActionsFor(dbContext, broker)
            .ReturnToSourceAsync(["m1"], "shipping-service", "operator", CancellationToken.None);

        // Assert
        Assert.Equal("shipping-service", broker.SentMessages.Single().Destination);
    }

    [Fact]
    public async Task DiscardAsync_WhenAFailureIsWrittenOff_KeepsTheRowAndTheReason()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        // Act
        await ActionsFor(dbContext, Broker("orders-service"))
            .DiscardAsync(["m1"], "operator", "duplicate order", CancellationToken.None);

        var failure = await dbContext
            .Failures
            .FindAsync("m1");

        // Assert
        Assert.Equal((FailureStatus.Discarded, "duplicate order"), (failure!.Status, failure.ResolutionReason));
    }

    [Fact]
    public async Task DiscardAsync_WhenNoReasonIsGiven_Throws()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var actions = ActionsFor(dbContext, Broker("orders-service"));

        // Act
        Task Act()
            => actions.DiscardAsync(["m1"], "operator", string.Empty, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(Act);
    }

    [Fact]
    public async Task EditAndRetryAsync_WhenTheBodyIsCorrected_DispatchesTheCorrectionWithANewId()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service");

        // Act
        await ActionsFor(dbContext, broker).EditAndRetryAsync(
            "m1",
            "{\"orderId\":\"corrected\"}",
            new Dictionary<string, string>(),
            "operator",
            "wrong order id",
            CancellationToken.None
        );

        // Assert
        Assert.NotEqual("m1", broker.SentMessages.Single().MessageId);
    }

    [Fact]
    public async Task EditAndRetryAsync_WhenTheBodyIsCorrected_KeepsTheOriginalUntouched()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1", payload: "{\"orderId\":\"wrong\"}"));

        // Act
        await ActionsFor(dbContext, Broker("orders-service")).EditAndRetryAsync(
            "m1",
            "{\"orderId\":\"corrected\"}",
            new Dictionary<string, string>(),
            "operator",
            "wrong order id",
            CancellationToken.None
        );

        var failure = await dbContext
            .Failures
            .FindAsync("m1");

        // Assert
        Assert.Equal("{\"orderId\":\"wrong\"}", Encoding.UTF8.GetString(failure!.Payload));
    }

    [Fact]
    public async Task EditAndRetryAsync_WhenTheCorrectionIsDispatched_SaysWhereItCameFrom()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service");

        // Act
        await ActionsFor(dbContext, broker).EditAndRetryAsync(
            "m1",
            "{\"orderId\":\"corrected\"}",
            new Dictionary<string, string>(),
            "operator",
            "wrong order id",
            CancellationToken.None
        );

        // Assert
        Assert.Equal("m1", broker.SentMessages.Single().Headers[FailureActionService.EditedFromHeader]);
    }

    [Fact]
    public async Task EditAndRetryAsync_WhenTheBodyDoesNotParse_RefusesBeforeDispatching()
    {
        // Arrange
        await using var dbContext = await fixture.CreateDatabaseAsync();
        await FailureQueryTests.SeedAsync(dbContext, FailureQueryTests.Failure("m1"));

        var broker = Broker("orders-service");
        var actions = ActionsFor(dbContext, broker);

        // Act
        Task Act() => actions.EditAndRetryAsync(
            "m1",
            "not json",
            new Dictionary<string, string>(),
            "operator",
            "wrong order id",
            CancellationToken.None
        );

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(Act);
    }

    private static InMemoryBroker Broker(params string[] queueNames)
    {
        var broker = new InMemoryBroker();

        foreach (var queueName in queueNames)
        {
            broker.DeclareQueue(queueName);
        }

        return broker;
    }

    private static FailureActionService ActionsFor(OperationsDbContext dbContext, InMemoryBroker broker)
        => new(
            dbContext,
            new TransportSenderProvider(new InMemoryTransport(broker)),
            TimeProvider.System
        );
}
