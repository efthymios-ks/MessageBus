namespace MessageBus.Transport.RabbitMq.Tests;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void WithDelayedMessageExchange_WhenAName_SetsTheProperty()
    {
        // Arrange
        var options = new RabbitMqOptions();

        // Act
        options.WithDelayedMessageExchange("delayed");

        // Assert
        Assert.Equal("delayed", options.DelayedMessageExchange);
    }

    [Fact]
    public void WithDelayedMessageExchange_WhenNull_Throws()
    {
        // Arrange
        var options = new RabbitMqOptions();

        // Act
        void Act()
            => options.WithDelayedMessageExchange(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WithDelayedMessageExchange_WhenBlank_Throws(string exchangeName)
    {
        // Arrange
        var options = new RabbitMqOptions();

        // Act
        void Act()
            => options.WithDelayedMessageExchange(exchangeName);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void DelayedMessageExchange_WhenNotSet_IsNull()
    {
        // Arrange
        var options = new RabbitMqOptions();

        // Assert
        Assert.Null(options.DelayedMessageExchange);
    }
}
