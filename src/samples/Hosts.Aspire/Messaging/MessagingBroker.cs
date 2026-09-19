using MessageBus.Hosts.Infrastructure;

namespace MessageBus.Hosts.Aspire.Messaging;

internal static class MessagingBroker
{
    public static MessagingResources AddRabbitMq(IDistributedApplicationBuilder builder)
    {
        // Importing definitions overwrites the password the container was started with, so the
        // generated one the AppHost would otherwise hand out is already wrong by the time the
        // broker is up. Both sides name the same pair instead, or every endpoint waits forever
        // on a broker refusing it.
        var userName = builder.AddParameter("messaging-username", "guest");
        var password = builder.AddParameter("messaging-password", "guest", secret: true);

        var rabbitMq = builder
            .AddRabbitMQ(ResourceNames.Broker, userName, password)
            .WithManagementPlugin()
            .WithDataVolume()
            .WithLifetime(ContainerLifetime.Persistent)
            .WithBindMount("rabbitmq/definitions.json", "/etc/rabbitmq/definitions.json")
            .WithBindMount("rabbitmq/rabbitmq.conf", "/etc/rabbitmq/conf.d/20-definitions.conf")
            .WithDockerDesktopGroup(ResourceNames.ContainerPrefix);

        return new(rabbitMq, ConnectionStringNames.RMQ, rabbitMq.GetEndpoint("management"));
    }

    public static MessagingResources AddAzureServiceBusEmulator(IDistributedApplicationBuilder builder)
    {
        // The emulator has no management API, so its entities are declared here and nowhere else.
        var serviceBus = builder
            .AddAzureServiceBus(ResourceNames.Broker)
            .RunAsEmulator(emulator => emulator
                .WithLifetime(ContainerLifetime.Persistent)
                .WithDockerDesktopGroup(ResourceNames.ContainerPrefix));

        // Aspire resource names take a prefix; the queue/topic names on the wire stay unadorned so
        // the app talks to plain "orders", "billing", "Orders.OrderPlaced.v1" etc.
        serviceBus.AddServiceBusQueue($"queue-{QueueNames.Orders}", QueueNames.Orders);
        serviceBus.AddServiceBusQueue($"queue-{QueueNames.Billing}", QueueNames.Billing);
        serviceBus.AddServiceBusQueue($"queue-{QueueNames.Error}", QueueNames.Error);
        serviceBus.AddServiceBusQueue($"queue-{QueueNames.Audit}", QueueNames.Audit);

        // One subscription per endpoint name — each forwards to that endpoint's own queue, so
        // one queue per endpoint stays true on a broker where a subscription is itself a
        // receive point.
        var orderPlaced = serviceBus.AddServiceBusTopic("topic-orders-orderplaced-v1", TopicNames.OrderPlaced);
        orderPlaced
            .AddServiceBusSubscription("sub-orders-orderplaced-v1-billing", QueueNames.Billing)
            .WithProperties(subscription => subscription.ForwardTo = QueueNames.Billing);
        orderPlaced
            .AddServiceBusSubscription("sub-orders-orderplaced-v1-orders", QueueNames.Orders)
            .WithProperties(subscription => subscription.ForwardTo = QueueNames.Orders);

        var orderBilled = serviceBus.AddServiceBusTopic("topic-billing-orderbilled-v1", TopicNames.OrderBilled);
        orderBilled
            .AddServiceBusSubscription("sub-billing-orderbilled-v1-orders", QueueNames.Orders)
            .WithProperties(subscription => subscription.ForwardTo = QueueNames.Orders);

        return new(serviceBus, ConnectionStringNames.ASB, ManagementEndpoint: null);
    }
}
