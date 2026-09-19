using Testcontainers.ServiceBus;

namespace MessageBus.Transport.AzureServiceBus.Tests;

/// <summary>
/// The Service Bus emulator, with its topology declared up front. The emulator has no management
/// API, so entities exist because its configuration says they do — which is the same position a
/// production namespace is in, and the reason the transport verifies rather than creates.
/// </summary>
public sealed class ServiceBusFixture : IAsyncLifetime
{
    private readonly ServiceBusContainer _container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
        .WithAcceptLicenseAgreement(true)
        .WithConfig(EmulatorConfiguration.Path)
        .Build();

    public string ConnectionString
        => _container.GetConnectionString();

    public async Task InitializeAsync()
        => await _container.StartAsync();

    public async Task DisposeAsync()
        => await _container.DisposeAsync();

    public AzureServiceBusOptions Options()
        => new() { ConnectionString = ConnectionString };
}

[CollectionDefinition(Name)]
public sealed class ServiceBusCollection : ICollectionFixture<ServiceBusFixture>
{
    public const string Name = "ServiceBus";
}
