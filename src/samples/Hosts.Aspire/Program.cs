using MessageBus.Hosts.Aspire;
using MessageBus.Hosts.Aspire.Messaging;
using MessageBus.Hosts.Infrastructure;

var builder = DistributedApplication.CreateBuilder(args);
var environmentName = builder.Environment.EnvironmentName;
var transport = builder.Configuration["Messaging:Transport"] ?? TransportKinds.RMQ;

var sqlPassword = builder.AddParameter("sql-password", "MessageBus!Local1", secret: true);

var sql = builder
    .AddSqlServer(ResourceNames.SqlServer, password: sqlPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDockerDesktopGroup(ResourceNames.ContainerPrefix);

var ordersDatabase = sql.AddDatabase(ResourceNames.OrdersDb, "OrdersDb");
var billingDatabase = sql.AddDatabase(ResourceNames.BillingDb, "BillingDb");
var operationsDatabase = sql.AddDatabase(ResourceNames.OperationsDb, "OperationsDb");

var sqlPad = builder
    .AddContainer(ResourceNames.SqlPad, "sqlpad/sqlpad")
    .WithImageTag("7")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithDockerDesktopGroup(ResourceNames.ContainerPrefix)
    .WithEnvironment("SQLPAD_AUTH_DISABLED", "true")
    .WithEnvironment("SQLPAD_AUTH_DISABLED_DEFAULT_ROLE", "admin")
    .WaitFor(sql);

foreach (var (id, database) in new[]
    {
        ("orders", "OrdersDb"),
        ("billing", "BillingDb"),
        ("operations", "OperationsDb")
    })
{
    sqlPad
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__name", database)
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__driver", "sqlserver")
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__host", ResourceNames.SqlServer)
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__port", "1433")
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__database", database)
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__username", "sa")
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__password", sqlPassword)
        .WithEnvironment($"SQLPAD_CONNECTIONS__{id}__sqlserverEncrypt", "false");
}

var seq = builder
    .AddSeq(ResourceNames.Seq)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDockerDesktopGroup(ResourceNames.ContainerPrefix);

// Keys are how Operations knows an endpoint is expected. Fixed here so the sample starts without a
// registration step; a deployment issues them and treats them as credentials.
const string ordersOperationsKey = "orders-key";
const string billingOperationsKey = "billing-key";

var messaging = string.Equals(transport, TransportKinds.ASB, StringComparison.OrdinalIgnoreCase)
    ? MessagingBroker.AddAzureServiceBusEmulator(builder)
    : MessagingBroker.AddRabbitMq(builder);

var operations = builder
    .AddProject<Projects.MessageBus_Operations_Web>(ResourceNames.OperationsWeb)
    .WithReference(operationsDatabase, ConnectionStringNames.OperationsDb)
    .WaitFor(operationsDatabase)
    .WithReference(messaging.Broker, messaging.ConnectionStringName)
    .WaitFor(messaging.Broker)
    .WithReference(seq, ConnectionStringNames.Seq)
    .WaitFor(seq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
    .WithEnvironment("Messaging__Transport", transport)
    .WithEnvironment($"Messaging__Operations__EndpointKeys__{QueueNames.Orders}", ordersOperationsKey)
    .WithEnvironment($"Messaging__Operations__EndpointKeys__{QueueNames.Billing}", billingOperationsKey);

var orders = builder
    .AddProject<Projects.Hosts_Orders>(ResourceNames.OrdersApi)
    .WithReference(ordersDatabase, ConnectionStringNames.OrdersDb)
    .WaitFor(ordersDatabase)
    .WithReference(messaging.Broker, messaging.ConnectionStringName)
    .WaitFor(messaging.Broker)
    .WithReference(seq, ConnectionStringNames.Seq)
    .WaitFor(seq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
    .WithEnvironment("Messaging__Transport", transport)
    .WithEnvironment("Messaging__Operations__BaseAddress", operations.GetEndpoint("http"))
    .WithEnvironment("Messaging__Operations__ApiKey", ordersOperationsKey)
    .WithEnvironment("Messaging__Explorer__Enabled", "true");

var billing = builder
    .AddProject<Projects.Hosts_Billing>(ResourceNames.BillingApi)
    .WithReference(billingDatabase, ConnectionStringNames.BillingDb)
    .WaitFor(billingDatabase)
    .WithReference(messaging.Broker, messaging.ConnectionStringName)
    .WaitFor(messaging.Broker)
    .WithReference(seq, ConnectionStringNames.Seq)
    .WaitFor(seq)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", environmentName)
    .WithEnvironment("Messaging__Transport", transport)
    .WithEnvironment("Messaging__Operations__BaseAddress", operations.GetEndpoint("http"))
    .WithEnvironment("Messaging__Operations__ApiKey", billingOperationsKey)
    .WithEnvironment("Messaging__Explorer__Enabled", "true");

if (messaging.ManagementEndpoint is { } managementEndpoint)
{
    // Each service verifies its bindings against the management endpoint. Its host port is
    // assigned by Aspire, which is why it is passed rather than derived from the AMQP one.
    orders.WithEnvironment("Messaging__RabbitMq__ManagementUrl", managementEndpoint);
    billing.WithEnvironment("Messaging__RabbitMq__ManagementUrl", managementEndpoint);
}

await builder.Build().RunAsync();
