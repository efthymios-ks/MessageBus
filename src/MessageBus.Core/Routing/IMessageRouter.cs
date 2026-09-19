namespace MessageBus.Core.Routing;

/// <summary>
/// Which endpoint a command goes to. Events are absent by design — subscribers decide what they
/// want, so nothing here has to know who is listening.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Null when unroutable, so startup validation can report every missing route at once instead
    /// of failing on the first send in production.
    /// </summary>
    string? GetDestination(Type messageType);
}
