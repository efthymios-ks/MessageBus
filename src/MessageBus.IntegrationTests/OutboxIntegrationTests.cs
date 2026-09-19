namespace MessageBus.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OutboxIntegrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task SendAsync_WhenTheCommandIsHandled_CommitsTheBusinessWrite()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        // Act
        await host.SendAsync(new PlaceOrder { OrderId = "order-1" });
        await IntegrationHost.WaitForAsync(() => Task.FromResult(log.Entries.Contains("handled:order-1")));

        var order = await host.QueryAsync(dbContext => dbContext
            .Orders
            .FindAsync("order-1")
            .AsTask());

        // Assert
        Assert.NotNull(order);
    }

    [Fact]
    public async Task SendAsync_WhenTheHandlerPublishes_TheEventReachesItsSubscriber()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        // Act
        await host.SendAsync(new PlaceOrder { OrderId = "order-2" });

        // Waits on the committed row, not on the log line: the handler logs before its transaction
        // commits, so a wait on the log would read the database mid-flight.
        await IntegrationHost.WaitForAsync(async () =>
        {
            var pending = await host.QueryAsync(dbContext => dbContext
                .Orders
                .FindAsync("order-2")
                .AsTask());

            return pending?.Status == "Observed";
        });

        var order = await host.QueryAsync(dbContext => dbContext
            .Orders
            .FindAsync("order-2")
            .AsTask());

        // Assert
        Assert.Equal("Observed", order!.Status);
    }

    [Fact]
    public async Task SendAsync_WhenEveryAttemptFails_LeavesNoBusinessWriteBehind()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        // Act
        await host.SendAsync(new FailOrder { OrderId = "order-3" });
        await IntegrationHost.WaitForAsync(() => Task.FromResult(log.Entries.Count(entry => entry == "attempt:order-3") == 2));

        var order = await host.QueryAsync(dbContext => dbContext
            .Orders
            .FindAsync("order-3")
            .AsTask());

        // Assert
        Assert.Null(order);
    }

    [Fact]
    public async Task SendAsync_WhenEveryAttemptFails_ForwardsTheMessageToTheErrorQueue()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        // Act
        await host.SendAsync(new FailOrder { OrderId = "order-4" });
        await IntegrationHost.WaitForAsync(() => Task.FromResult(host.Broker.QueueDepth("messagebus-error") == 1));

        // Assert
        Assert.Equal(1, host.Broker.QueueDepth("messagebus-error"));
    }

    [Fact]
    public async Task SendAsync_WhenTheSameMessageArrivesTwice_TheHandlerRunsOnce()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        await host.SendAsync(new PlaceOrder { OrderId = "order-5" });
        await IntegrationHost.WaitForAsync(() => Task.FromResult(log.Entries.Contains("observed:order-5")));

        // Act
        var delivered = host.Broker.SentMessages.First(message =>
            message.MessageTypeName.EndsWith("PlaceOrder", StringComparison.Ordinal));

        host.Broker.Redeliver(delivered);

        await IntegrationHost.WaitForAsync(() => Task.FromResult(host.Broker.IsIdle));

        // Assert
        Assert.Single(log.Entries, entry => entry == "handled:order-5");
    }

    [Fact]
    public async Task SendAsync_WhenTheRelayHasDispatched_MarksTheOutboxRow()
    {
        // Arrange
        var log = new TestLog();
        await using var host = await IntegrationHost.StartAsync(fixture.NewConnectionString(), log);

        // Act
        await host.SendAsync(new PlaceOrder { OrderId = "order-6" });
        await IntegrationHost.WaitForAsync(() => Task.FromResult(log.Entries.Contains("observed:order-6")));

        await IntegrationHost.WaitForAsync(async () => await host.QueryAsync(async dbContext
            => !await dbContext
                .Database
                .SqlQuery<bool>($"SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM messaging.Outbox WHERE IsDispatched = 0) THEN 1 ELSE 0 END AS bit) AS Value")
                .SingleAsync()));

        // Assert
        Assert.Contains("observed:order-6", log.Entries);
    }
}
