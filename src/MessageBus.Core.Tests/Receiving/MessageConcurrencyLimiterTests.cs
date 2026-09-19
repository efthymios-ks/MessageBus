using MessageBus.Core.Configuration;
using MessageBus.Core.Receiving;
using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Tests.Receiving;

public sealed class MessageConcurrencyLimiterTests
{
    [Fact]
    public async Task AcquireAsync_WhenTheTypeHasNoLimit_ReturnsNoLease()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 10, messageLimit: null);

        // Act
        using var lease = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Assert
        Assert.Null(lease);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheTypeIsLimitedAndFree_AcquiresALease()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 10, messageLimit: 1);

        // Act
        using var lease = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Assert
        Assert.True(lease!.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheLimitIsTaken_QueuesRatherThanRejecting()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 10, messageLimit: 1);
        using var held = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Act
        var queued = limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Assert
        Assert.False(queued.IsCompleted);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheQueueIsFull_ReturnsALeaseThatWasNotAcquired()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 1, messageLimit: 1);
        using var held = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        var queued = limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Act
        using var rejected = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Assert
        Assert.False(rejected!.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheMessageLimitExceedsTheEndpointLimit_CapsAtTheEndpointLimit()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 1, messageLimit: 50);
        using var held = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        var queued = limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Act
        using var rejected = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Assert
        Assert.False(rejected!.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_WhenALeaseIsReleased_LetsTheNextMessageThrough()
    {
        // Arrange
        using var limiter = CreateLimiter(endpointLimit: 10, messageLimit: 1);
        var held = await limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        var queued = limiter.AcquireAsync(NameOf<PlaceOrder>(), CancellationToken.None);

        // Act
        held!.Dispose();

        using var granted = await queued;

        // Assert
        Assert.True(granted!.IsAcquired);
    }

    private static string NameOf<TMessage>()
        => typeof(TMessage).FullName!;

    private static MessageConcurrencyLimiter CreateLimiter(int endpointLimit, int? messageLimit)
    {
        var options = new MessagingOptions { EndpointName = "test-endpoint" };

        options.Receiver.MaxConcurrentMessages = endpointLimit;

        if (messageLimit is { } limit)
        {
            options.MessageSettings[typeof(PlaceOrder)] = new MessageSettings { MaxConcurrentMessages = limit };
        }

        return new MessageConcurrencyLimiter(
            options,
            new FullNameMessageTypeResolver([typeof(PlaceOrder).Assembly])
        );
    }
}
