using MessageBus.Core.Configuration;

namespace MessageBus.Core.Tests.Configuration;

public sealed class RetryPolicyTests
{
    [Fact]
    public void None_WhenAsked_AllowsASingleAttempt()
    {
        // Arrange
        var policy = RetryPolicy.None;

        // Act
        var maxAttempts = policy.MaxAttempts;

        // Assert
        Assert.Equal(1, maxAttempts);
    }

    [Fact]
    public void DelayBefore_WhenPolicyIsFixed_ReturnsTheSameDelayForEveryAttempt()
    {
        // Arrange
        var policy = RetryPolicy.Fixed(maxAttempts: 5, TimeSpan.FromSeconds(2));

        // Act
        var delays = new[] { policy.DelayBefore(1), policy.DelayBefore(3), policy.DelayBefore(5) };

        // Assert
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromSeconds(2), delay));
    }

    [Fact]
    public void DelayBefore_WhenPolicyIsExponential_DoublesEachAttempt()
    {
        // Arrange
        var policy = RetryPolicy.Exponential(maxAttempts: 5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        // Act
        var delays = new[] { policy.DelayBefore(1), policy.DelayBefore(2), policy.DelayBefore(3) };

        // Assert
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delays);
    }

    [Fact]
    public void DelayBefore_WhenTheDoublingPassesTheCeiling_StopsAtMaxDelay()
    {
        // Arrange
        var policy = RetryPolicy.Exponential(maxAttempts: 20, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

        // Act
        var delay = policy.DelayBefore(attempt: 15);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void DelayBefore_WhenTheAttemptIsHighEnoughToOverflowTicks_StillReturnsMaxDelay()
    {
        // Arrange
        var policy = RetryPolicy.Exponential(maxAttempts: 100, TimeSpan.FromDays(1), TimeSpan.FromDays(7));

        // Act
        var delay = policy.DelayBefore(attempt: 90);

        // Assert
        Assert.Equal(TimeSpan.FromDays(7), delay);
    }

    [Fact]
    public void Exponential_WhenMaxDelayIsBelowTheInitialDelay_Throws()
    {
        // Arrange
        var initialDelay = TimeSpan.FromSeconds(10);

        // Act
        void Act()
            => RetryPolicy.Exponential(maxAttempts: 3, initialDelay, TimeSpan.FromSeconds(1));

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }

    [Fact]
    public void DelayBefore_WhenTheAttemptIsBelowOne_Throws()
    {
        // Arrange
        var policy = RetryPolicy.Fixed(maxAttempts: 3, TimeSpan.Zero);

        // Act
        void Act()
            => policy.DelayBefore(attempt: 0);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(Act);
    }
}
