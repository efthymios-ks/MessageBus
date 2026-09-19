using MessageBus.Core.TypeResolution;

namespace MessageBus.Core.Tests.TypeResolution;

public sealed class TypeNamingTests
{
    [Fact]
    public void NameOf_WhenTypeIsANormalClass_ReturnsNamespacePlusName()
    {
        // Act
        var name = TypeNaming.NameOf(typeof(SampleContract));

        // Assert
        Assert.Equal("MessageBus.Core.Tests.TypeResolution.TypeNamingTests+SampleContract", name);
    }

    [Fact]
    public void NameOf_WhenTypeIsFrameworkPrimitive_ReturnsFrameworkFullName()
    {
        // Act
        var name = TypeNaming.NameOf(typeof(string));

        // Assert
        Assert.Equal("System.String", name);
    }

    [Fact]
    public void NameOf_WhenTypeIsNested_UsesPlusSeparator()
    {
        // Act
        var name = TypeNaming.NameOf(typeof(SampleContract.Inner));

        // Assert
        Assert.Equal("MessageBus.Core.Tests.TypeResolution.TypeNamingTests+SampleContract+Inner", name);
    }

    [Fact]
    public void NameOf_WhenTypeIsClosedGeneric_MatchesTheOpenTypeArityAndArgument()
    {
        // Act
        var name = TypeNaming.NameOf(typeof(List<int>));

        // Assert — the assembly-info tail (version, culture, public key token) varies with the
        // runtime, so match the stable structure: open type + arity, then the type argument.
        Assert.Matches(@"^System\.Collections\.Generic\.List`1\[\[System\.Int32, .+\]\]$", name);
    }

    [Fact]
    public void NameOf_WhenTypeIsNull_ThrowsArgumentNullException()
    {
        // Act
        var exception = Record.Exception(() => TypeNaming.NameOf(null!));

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
    }

    [Fact]
    public void NameOf_WhenTypeHasNoFullName_ThrowsInvalidOperationException()
    {
        // Arrange — an open generic type parameter has null FullName.
        var openParameter = typeof(List<>).GetGenericArguments()[0];

        // Act
        var exception = Record.Exception(() => TypeNaming.NameOf(openParameter));

        // Assert
        var operation = Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal($"'{openParameter.Name}' has no full name to store under.", operation.Message);
    }

    public sealed class SampleContract
    {
        public sealed class Inner;
    }
}
