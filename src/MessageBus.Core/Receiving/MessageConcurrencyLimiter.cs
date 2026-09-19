using System.Collections.Frozen;
using System.Threading.RateLimiting;
using MessageBus.Core.Configuration;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Receiving;

/// <summary>
/// The per-message-type ceilings. Keyed by wire name so a limiter is found without deserializing
/// anything, and built once because the set of limited types is fixed at registration.
/// </summary>
internal sealed class MessageConcurrencyLimiter : IDisposable
{
    private readonly FrozenDictionary<string, ConcurrencyLimiter> _limiters;

    public MessageConcurrencyLimiter(MessagingOptions options, IMessageTypeResolver typeResolver)
    {
        var endpointLimit = options.Receiver.MaxConcurrentMessages;

        _limiters = options.MessageSettings
            .Where(entry => entry.Value.MaxConcurrentMessages is > 0)
            .ToFrozenDictionary(
                entry => typeResolver.GetMessageTypeName(entry.Key),
                entry => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    // Capped, never raised: the message already passed the endpoint-wide gate, so a
                    // value above it could only ever be a no-op that reads as a promise.
                    PermitLimit = Math.Min(entry.Value.MaxConcurrentMessages!.Value, endpointLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,

                    // Small on purpose. An unbounded wait lets a bulk arrival of one limited type
                    // hold every global slot while other types sit unread — a stall in all but name.
                    QueueLimit = endpointLimit
                }),
                StringComparer.Ordinal
            );
    }

    /// <summary>
    /// A lease that was not acquired means the message should go back to the broker rather than
    /// wait: redelivery frees the global slot immediately, and the inbox already makes it harmless.
    /// </summary>
    public ValueTask<RateLimitLease?> AcquireAsync(string messageTypeName, CancellationToken cancellationToken)
        => _limiters.TryGetValue(messageTypeName, out var limiter)
            ? AcquireCoreAsync(limiter, cancellationToken)
            : ValueTask.FromResult<RateLimitLease?>(null);

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }

    private static async ValueTask<RateLimitLease?> AcquireCoreAsync(
        ConcurrencyLimiter limiter,
        CancellationToken cancellationToken
    ) => await limiter.AcquireAsync(permitCount: 1, cancellationToken);
}
