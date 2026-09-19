# Observability

Correlation ids, metrics, headers, OpenTelemetry.

## Three ids

| Id | Constant for |
| --- | --- |
| `message-id` | one message. The deduplication key. |
| `correlation-id` | the whole business flow. |
| `causation-id` | the message that produced this one. |

- A flow starts where a message is created without an incoming one.
- `correlation-id` is seeded from the ambient trace id, so an HTTP request and everything it causes share one id.
- Inside a handler, `IMessageContext` propagates all three automatically.
- There is no unpropagated way to send from a handler. That is the point.
- `traceparent` is stamped when the outbox row is **written**, not when the relay transmits it. Otherwise every consumer becomes a child of the relay loop.

## OpenTelemetry

```csharp
.WithMetrics(metrics => metrics.AddMeter(MessagingDiagnostics.MeterName))
.WithTracing(tracing => tracing.AddSource(MessagingDiagnostics.ActivitySourceName))
```

## Metrics

| Instrument | Counts |
| --- | --- |
| `messagebus.messages.sent` | outbox rows written |
| `messagebus.messages.dispatched` | rows the relay moved onto the transport |
| `messagebus.messages.handled` | handled and acknowledged |
| `messagebus.messages.failed` | retries exhausted, moved to error queue |
| `messagebus.messages.retried` | attempts that failed and were retried |
| `messagebus.messages.duplicates` | redeliveries the inbox discarded |
| `messagebus.messages.dead_lettered` | unreadable, unknown or unhandled |
| `messagebus.message.duration` | histogram, ms spent handling |

Tagged with `messaging.message_type` and `messaging.endpoint`.
Not split per type — a new type widens a dashboard instead of needing a new panel.

## Headers reference

Full list: `src/MessageBus.Core/Dispatching/MessageHeaders.cs`.
The ones every message carries: `message-id`, `message-type`, `message-clr-type`, `message-intent`, `content-type`, `correlation-id`, `originator`, `sent-at`, `traceparent`.
