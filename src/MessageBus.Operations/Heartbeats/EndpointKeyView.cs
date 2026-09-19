namespace MessageBus.Operations.Heartbeats;

/// <summary>One row on the Keys screen.</summary>
/// <param name="EndpointName">The endpoint the key belongs to.</param>
/// <param name="ApiKey">The plaintext key. Rendered in the Edit modal, never in the list itself.</param>
/// <param name="RegisteredAt">When the registration was created.</param>
/// <param name="StaleAfter">How long silence is tolerated before an instance reads as stale.</param>
/// <param name="Disabled">True when the key is refused at the heartbeats endpoint.</param>
/// <param name="LastSeenMachine">Machine of the most recent instance, or null if none reported.</param>
/// <param name="LastSeenAt">When any instance under this endpoint last reported, or null if none.</param>
public sealed record EndpointKeyView(
    string EndpointName,
    string ApiKey,
    DateTimeOffset RegisteredAt,
    TimeSpan StaleAfter,
    bool Disabled,
    string? LastSeenMachine,
    DateTimeOffset? LastSeenAt
);
