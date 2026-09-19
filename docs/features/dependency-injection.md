# Dependency injection

Every builder call in one place, comment on the same line as the call it describes.
Per-topic depth lives in the feature docs — this one is the catalogue.

## The whole chain

```csharp
builder.Services
    .AddMessaging("orders")                                                                 // Register messaging services. Endpoint name is the queue name.

    .WithRabbitMqTransport(rabbitMq => rabbitMq                                             // Transport. Alternatives commented out below.
        .WithConnectionString(builder.Configuration.GetConnectionString("messaging")!)      // AMQP URI. Required.
        .WithClientProvidedName("orders-service")                                           // Optional connection name shown in the broker's UI.
        .WithExchangePrefix("prod.")                                                        // Optional prefix on every exchange this endpoint publishes to.
        .WithVirtualHost("prod")                                                            // Optional non-default vhost.
        .WithManagementUrl(builder.Configuration["RabbitMq:ManagementUrl"]!))               // Enables binding verification via the management HTTP API. Off by default.

    //  .WithAzureServiceBusTransport(serviceBus => serviceBus                              // Alternative transport.
    //      .WithConnectionString(builder.Configuration.GetConnectionString("messaging")!)  // Namespace connection string with send + listen rights.
    //      .WithTopicPrefix("prod.")                                                       // Optional prefix on every topic.
    //      .WithReceiveBatchSize(20)                                                       // How many messages one receive call asks for. Default 10.
    //      .WithSubscriptionVerification())                                                // Verifies topics + subscriptions through the admin API. Needs manage rights.

    .WithEntityFrameworkCorePersistence<OrdersDbContext>(efCore => efCore                   // Persistence. Adds Outbox / Inbox / DelayedMessages / Sagas onto the host's DbContext.
        .WithoutPendingMigrationsCheck()                                                    // Skip the migrations preflight. Only when a deploy step guarantees the schema.
        .WithInboxRetention(TimeSpan.FromDays(7))                                           // How long a processed inbox row is kept before the prune service deletes it.
        .WithOutboxRetention(TimeSpan.FromDays(1))                                          // How long a dispatched outbox row is kept for diagnostics before pruning.
        .WithPruneInterval(TimeSpan.FromHours(1)))                                          // How often the prune loop runs.

    .WithJsonSerializer()                                                                   // Default serializer.
    //  .WithJsonSerializer(json => json.Converters.Add(new MoneyJsonConverter()))          // JSON with extra converters.
    //  .WithMessagePackSerializer()                                                        // MessagePack instead of JSON.
    //  .WithSerializer<TSerializer>()                                                      // Your own IMessageSerializer.

    .WithMessageTypeMap(typeMap => typeMap                                                  // Type resolver — explicit map.
        .Map<OrderPlaced>("Orders.OrderPlaced.v1")                                          // Explicit entry — wire name picked by hand.
        .MapAttributedAssemblyOf<PlaceOrder>()                                              // Reads [MessageName("...")] off every attributed type in the marker's assembly.
        .MapAssemblyOf<ShipOrder>(type => $"Shipping.{type.Name}.v1"))                      // Assembly-wide naming function.
    //  .WithFullNameMessageTypeResolver<OrderPlaced>()                                     // Alternative: use each type's CLR full name as the wire name.

    .WithMessageRouting(routes => routes                                                    // Routing — explicit map.
        .Map<ReserveStock>("inventory-service")                                             // One command to one endpoint.
        .MapAssemblyOf<ShipOrder>("shipping-service"))                                      // Every command in the assembly to one endpoint.
    //  .WithAttributeMessageRouting()                                                      // Alternative: reads [MessageDestination("...")] off contracts.
    //  .WithMessageRouter<TenantMessageRouter>()                                           // Alternative: custom IMessageRouter for config/tenant/dynamic routing.

    .WithMessageHandlersFromAssemblyOf<OrderSaga>()                                         // Scan for IMessageHandler<T> and saga implementations.
    //  .WithMessageHandlers(typeof(PlaceOrderHandler), typeof(OrderSaga))                  // Alternative: hand-list.
    //  .WithMessageHandlersFromEntryAssembly()                                             // Alternative: scan the entry assembly.

    .WithReceiver(receiver => receiver                                                      // Receiver knobs — endpoint-wide.
        .WithMaxConcurrentMessages(10)                                                      // Max concurrent messages across every type on this endpoint.
        .WithPrefetchCount(50))                                                             // How many messages the transport pulls into memory at once.

    .WithDefaultHandlerTimeout(TimeSpan.FromSeconds(30))                                    // Endpoint-wide default; every message type inherits it.
    .WithDefaultRetryPolicy(RetryPolicy.Exponential(                                        // Endpoint-wide default.
        maxAttempts: 5,
        initialDelay: TimeSpan.FromSeconds(1),
        maxDelay: TimeSpan.FromMinutes(1)))

    .WithMessageOptions<GenerateMonthlyReport>(message => message                           // Per-message overrides — set only what differs from defaults.
        .WithHandlerTimeout(TimeSpan.FromMinutes(10))                                       // Long-running — override the 30s default.
        .WithMaxConcurrentMessages(1)                                                       // Serialized — never run two of these at once.
        .WithoutRetries())                                                                  // A retry cannot help; go straight to the error queue.

    .WithIncomingBehavior<ValidationBehavior>()                                             // Custom pipeline slot on the incoming (logical) stage.
    .WithOutgoingBehavior<TenantHeaderBehavior>()                                           // Custom pipeline slot on the outgoing stage.

    .WithOutboxRelay(outbox =>                                                              // Hosted service that moves outbox rows onto the transport.
    {
        outbox.BatchSize = 200;                                                             // How many rows per claim.
        outbox.PollingInterval = TimeSpan.FromSeconds(5);                                   // How often the relay wakes when no signal has arrived.
    })

    .WithDelayedDeliveryRelay(delayed =>                                                    // Hosted service that promotes due delayed messages into the outbox.
    {
        delayed.BatchSize = 200;
        delayed.PollingInterval = TimeSpan.FromSeconds(5);
    })

    .WithErrorQueue(error => error.WithQueueName("messagebus-error"))                       // Where retry-exhausted messages go. Default name: messagebus-error.

    .WithAuditQueue(audit => audit                                                          // Copy of every processed message. Off by default; doubles broker traffic.
        .WithQueueName("messagebus-audit")                                                  // Default name: messagebus-audit.
        .Enabled(builder.Environment.IsProduction()))                                       // Predicate over any state.

    .WithOperationsReporting(operations =>                                                  // Report to Operations so it lists this endpoint as live.
    {
        operations.WithBaseAddress(builder.Configuration["Operations:BaseAddress"]!);       // Operations HTTP endpoint. Required.
        operations.WithApiKey(builder.Configuration["Operations:ApiKey"]!);                 // Api key issued from the /Keys screen. Required.
        operations.InstanceId = builder.Configuration["POD_NAME"];                          // Optional. Defaults to Environment.MachineName.
        operations.Version = typeof(Program).Assembly.GetName().Version?.ToString();        // Optional. Shown on /Endpoints.
        operations.Interval = TimeSpan.FromSeconds(30);                                     // Optional. Server may override with its own cadence.
    });
```

## Model builder

Runs once, in your `DbContext.OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyMessagingModel(model =>                       // Adds Outbox / Inbox / DelayedMessages / Sagas.
    {
        model.WithSchema("Messaging");                              // Schema for all four tables. Default: "Messaging".
        model.WithOutboxTable("Outbox");                            // Override per table if you need to fit an existing convention.
        model.WithInboxTable("Inbox");
        model.WithDelayedTable("DelayedMessages");
        model.WithSagaTable("Sagas");
        model.WithOptimisticConcurrencyForSagas();                  // Off by default (provider portability). Turn on for production. See sagas.md#concurrency.
        model.WithMaxPayloadLength(1_000_000);                      // Cap on Payload/Headers/State. Unbounded by default.
    });
}
```

## Host startup

Runs once, before `RunAsync`:

```csharp
var app = builder.Build();

await app.UseMessagingAsync();                                      // Validates config + verifies broker topology. Throws with every gap named at once.

await app.RunAsync();
```

## Operations backend

The `MessageBus.Operations` deployable — not an endpoint, but registers services of its own:

```csharp
builder.Services.AddRabbitMqTransport(rabbitMq => rabbitMq          // Just the transport — Operations does not use the messaging pipeline itself.
    .WithConnectionString(connectionString));

builder.Services.AddMessagingOperations(operations => operations    // Ingestion services + storage.
    .WithErrorQueueName("messagebus-error")                         // Shared error queue name.
    .WithAuditQueueName("messagebus-audit")                         // Setting one also turns audit ingestion on.
    .WithAuditRetention(TimeSpan.FromDays(7)));                     // How long an audit row is kept before OperationsPruneService deletes it.
```

## OpenTelemetry

Registers the meter and activity source shipped by the library:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMessaging())                 // messagebus.messages.* counters + duration histogram.
    .WithTracing(tracing => tracing.AddMessaging());                // Consumer + producer spans.
```

## Explorer

Per-host `/messages` browser for sending any handled message by hand:

```csharp
app.MapMessageExplorer(configuration =>                             // Predicate over configuration, not an environment name.
    configuration.GetValue("Messaging:Explorer:Enabled", false));   // Return false to map nothing.
```

## Testing

Same fluent shape, in-memory pieces:

```csharp
builder.Services
    .AddMessaging("test-endpoint")
    .WithInMemoryTransport()                                        // Private broker per test. Overload accepts a shared InMemoryBroker.
    .WithInMemoryPersistence()                                      // Private in-memory store. Overload accepts an existing InMemoryMessageStore.
    .WithFullNameMessageTypeResolver<PlaceOrder>()
    .WithMessageRouting(routes => routes.MapAssemblyOf<PlaceOrder>("test-endpoint"))
    .WithMessageHandlers(typeof(PlaceOrderHandler));
```

Or use `MessagingTestHarness` which wires all of that in one call — see [testing.md](testing.md).

## Where to look for depth

- Contracts, wire names, routing — [messages-and-routing.md](messages-and-routing.md)
- Dispatch, transactions, delayed delivery — [sending-and-handling.md](sending-and-handling.md)
- Retries, timeouts, concurrency — [reliability.md](reliability.md)
- Error / audit forwarding + ingestion — [error-and-audit-queues.md](error-and-audit-queues.md)
- Transports and topology — [transports.md](transports.md)
- Persistence + tables — [persistence-and-outbox.md](persistence-and-outbox.md), [storage-tables.md](storage-tables.md)
- Sagas — [sagas.md](sagas.md)
- Custom behaviors — [pipeline-and-behaviors.md](pipeline-and-behaviors.md)
- Background jobs — [background-jobs.md](background-jobs.md)
- Serialization — [serialization.md](serialization.md)
- Observability — [observability.md](observability.md)
- Operations service — [operations.md](operations.md)
- Message Explorer — [message-explorer.md](message-explorer.md)
- Deployment — [deployment.md](deployment.md)
- Testing — [testing.md](testing.md)
