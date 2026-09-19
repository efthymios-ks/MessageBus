# Transports

RabbitMQ, Azure Service Bus, in-memory.
All implement `IMessageTransport`.
The pipeline does not care which one runs.

## RabbitMQ

```csharp
.WithRabbitMqTransport(rabbitMq => rabbitMq
    .WithConnectionString(connectionString)
    .WithManagementUrl(builder.Configuration["RabbitMq:ManagementUrl"]!))
```

- One queue per endpoint.
- Events publish to an exchange named after the wire type.
- Subscriptions are bindings from that exchange to the endpoint queue.
- Management URL is optional but recommended — see [topology](#topology).

### Delayed delivery on the broker

RabbitMQ has no native scheduling.
The `rabbitmq_delayed_message_exchange` plugin adds an `x-delayed-message` exchange type that holds each message for its `x-delay` header and then routes it normally.

Opt in by pointing the transport at an exchange the operator has declared:

```csharp
.WithRabbitMqTransport(rabbitMq => rabbitMq
    .WithConnectionString(connectionString)
    .WithDelayedMessageExchange("delayed"))
```

- Turns on `ITransportSender.SupportsDelayedDelivery`, so [`DelayedDeliveryRelay`](background-jobs.md#delayeddeliveryrelay) with `WithTransportDelayWhenAvailable(...)` hands short delays to the broker instead of round-tripping through the `DelayedMessages` table.
- The exchange must exist before startup — `VerifyTopologyAsync` passive-declares it and fails fast when missing, same as any other queue or exchange.
- Routing keys mirror the immediate path: queue name for commands, event wire name for events. The operator's bindings on the delayed exchange stay symmetrical with the direct + fanout topology.
- A message whose delivery time has already passed by the time the relay reaches the broker takes the immediate path — no wasted round trip through the plugin.
- Unset: delayed messages travel through the framework's `DelayedDeliveryRelay` and reach the broker as ordinary sends when they come due.

## Azure Service Bus

```csharp
.WithAzureServiceBusTransport(serviceBus => serviceBus
    .WithConnectionString(connectionString)
    .WithSubscriptionVerification())
```

- One queue per endpoint.
- Events publish to a topic named after the wire type.
- Every subscription forwards to its endpoint's queue.
- The pump only ever reads one entity.
- Subscription verification needs manage rights.

### Delayed delivery on the broker

Service Bus ships with native scheduling.
`SupportsDelayedDelivery` is always true and `TransmitDelayedAsync` sets `ScheduledEnqueueTime` on the outgoing message — no header, no separate exchange.
Nothing to configure.

## In-memory

Used by `MessageBus.Testing`.
Implements the same `ITransportSender` / `ITransportReceiver` as the real ones.
A double that skipped the pipeline would test nothing worth testing.

## Topology

Queues, topics, subscriptions exist before the process starts.
Created by whoever owns the environment.
`VerifyTopologyAsync` names everything missing at once and fails startup.

Adding a handler for a new event means adding the entity to the environment first.
That friction is the point.
An application that creates its own topology means the real topology lives in code that ran once, months ago, on a machine nobody has.

## Fan-out verification

Neither broker verifies fan-out wiring over AMQP.
RabbitMQ has no passive binding declare.
A Service Bus subscription that forwards to the endpoint queue refuses every read.
Both therefore go through a management API when configured.

Without either: queues, exchanges and topics are still verified.
But a queue bound to the wrong exchange, or a topic nobody subscribed this endpoint to, passes startup.
Surfaces later as an event that silently never arrives.
