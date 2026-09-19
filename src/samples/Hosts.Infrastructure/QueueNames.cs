namespace MessageBus.Hosts.Infrastructure;

public static class QueueNames
{
    public const string Orders = "orders";

    public const string Billing = "billing";

    public const string Error = "messagebus-error";

    public const string Audit = "messagebus-audit";
}
