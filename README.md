# MessageBus

Commands and events, handlers and sagas, outbox and inbox, retries and delayed delivery.
Broker and database behind interfaces.

## Project layout

```
src/MessageBus.Abstractions                     contracts application code writes against
src/MessageBus.Core                             pipeline, pump, dispatcher, relays, builder, SPI
src/MessageBus.Persistence.EntityFrameworkCore  outbox, inbox, delayed, sagas
src/MessageBus.Transport.RabbitMq               bytes ↔ broker
src/MessageBus.Transport.AzureServiceBus        bytes ↔ broker
src/MessageBus.Serialization.MessagePack        MessagePack instead of JSON
src/MessageBus.Operations                       failure storage, ingestion, retry, flow view
src/MessageBus.Operations.Client                endpoint-side heartbeats
src/MessageBus.Operations.Web                   the Operations UI
src/MessageBus.Explorer                         send any handled message from a browser
src/MessageBus.Testing                          in-memory transport and persistence
src/samples/                                    Aspire AppHost, two endpoints, shared contracts
```

## Docs

- [Getting started](docs/features/getting-started.md) — project layout, five interfaces, minimum viable endpoint.
- [Dependency injection](docs/features/dependency-injection.md) — every public builder / DI extension a host uses, per package.
- [Messages & routing](docs/features/messages-and-routing.md) — commands vs events, wire names, three ways to route.
- [Sending & handling](docs/features/sending-and-handling.md) — bus, context, handlers, delayed delivery, transactions.
- [Sagas](docs/features/sagas.md) — state, correlation, completion.
- [Persistence & outbox](docs/features/persistence-and-outbox.md) — four tables, relays, EF Core migrations.
- [Storage tables](docs/features/storage-tables.md) — schema for every messaging and Operations table.
- [Pipeline & behaviors](docs/features/pipeline-and-behaviors.md) — physical vs logical stages, custom slots.
- [Reliability](docs/features/reliability.md) — retries, timeouts, concurrency, no-handler short-circuit.
- [Background jobs](docs/features/background-jobs.md) — every hosted service: pump, relays, prune, heartbeats, ingestion.
- [Error & audit queues](docs/features/error-and-audit-queues.md) — what gets forwarded, what gets ingested.
- [Serialization](docs/features/serialization.md) — JSON default, MessagePack, custom.
- [Transports](docs/features/transports.md) — RabbitMQ, Azure Service Bus, in-memory, topology.
- [Observability](docs/features/observability.md) — correlation ids, metrics, headers, OpenTelemetry.
- [Testing](docs/features/testing.md) — harness, in-memory transport, suite topology.
- [Operations](docs/features/operations.md) — the admin service (heartbeats + UI).
- [Message Explorer](docs/features/message-explorer.md) — per-host `/messages` page.
- [Deployment](docs/features/deployment.md) — Linux runtime, Docker, Aspire dev orchestration.

## License

MIT.
