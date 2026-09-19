namespace MessageBus.Hosts.Aspire.Messaging;

internal sealed record MessagingResources(
    IResourceBuilder<IResourceWithConnectionString> Broker,
    string ConnectionStringName,
    EndpointReference? ManagementEndpoint
);
