namespace MessageBus.Core.Startup;

/// <summary>
/// A precondition a provider package adds to <see cref="HostExtensions.UseMessagingAsync"/>. Resolved from a scope and
/// run before the host starts, so whatever it finds is an exit code rather than a failure on the
/// first message.
/// </summary>
public interface IMessagingStartupCheck
{
    /// <summary>
    /// Every problem found, not the first. One restart should be enough to see the whole gap.
    /// </summary>
    Task<IReadOnlyList<string>> FindProblemsAsync(CancellationToken cancellationToken);
}
