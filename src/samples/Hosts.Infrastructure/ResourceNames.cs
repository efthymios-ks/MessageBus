namespace MessageBus.Hosts.Infrastructure;

/// <summary>Aspire dashboard resource names, referenced from both the AppHost and the endpoint hosts.</summary>
public static class ResourceNames
{
    /// <summary>Shared container-name prefix so every container this AppHost owns groups together in Docker Desktop.</summary>
    public const string ContainerPrefix = "messagebus";

    public const string SqlServer = "sql-server";

    public const string OrdersDb = "db-orders";

    public const string BillingDb = "db-billing";

    public const string OperationsDb = "db-operations";

    public const string OrdersApi = "api-orders";

    public const string BillingApi = "api-billing";

    public const string OperationsWeb = "web-operations";

    public const string Broker = "broker-messaging";

    public const string Seq = "seq";

    public const string SqlPad = "sqlpad";
}
