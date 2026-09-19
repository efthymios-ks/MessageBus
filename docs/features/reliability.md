# Reliability

Retries, handler timeout, concurrency.
Endpoint defaults + per-message overrides.

## Retry policy

```csharp
.WithDefaultRetryPolicy(RetryPolicy.Exponential(
    maxAttempts: 5,
    initialDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromMinutes(1)))
```

- The retry policy owns when retries stop.
- A message that exhausts retries is forwarded to the error queue with exception headers.
- The endpoint keeps nothing.
- Use `RetryPolicy.None` for a message a retry cannot help.

## Handler timeout

```csharp
.WithDefaultHandlerTimeout(TimeSpan.FromSeconds(30))
```

`TimeoutBehavior` swaps the timeout token onto `context.CancellationToken`.
Any handler that respects the token respects the timeout.

## Per-message overrides

```csharp
.WithMessageOptions<GenerateMonthlyReport>(message => message
    .WithHandlerTimeout(TimeSpan.FromMinutes(10))
    .WithMaxConcurrentMessages(1)
    .WithoutRetries())
```

Null on any field = "use the endpoint default".
Setting one overrides it for that type only.

## Concurrency

```csharp
.WithReceiver(receiver => receiver
    .WithMaxConcurrentMessages(10)
    .WithPrefetchCount(50))
```

- **Endpoint-wide**: `ReceiverSettings.MaxConcurrentMessages`, default 10.
- Enforced by `Parallel.ForEachAsync` `MaxDegreeOfParallelism` in `MessagePump`.
- **Per-message-type**: `MessageSettings.MaxConcurrentMessages`.
- Enforced by `MessageConcurrencyLimiter`.
- A per-type value above the endpoint limit is capped, not raised.
- **At capacity**: a message whose type is at its cap is abandoned back to the broker for redelivery.
- Not queued in-process.
- Prevents one type's burst from holding every global slot while other types sit unread.

## No handler for message type

`NoHandlerForMessageException` short-circuits the pipeline.
The message is forwarded to the error queue immediately.
No retries — a retry cannot conjure a handler.
