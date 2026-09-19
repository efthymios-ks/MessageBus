namespace MessageBus.Operations.Tests;

[CollectionDefinition(Name)]
public sealed class OperationsCollection : ICollectionFixture<OperationsFixture>
{
    public const string Name = "Operations";
}
