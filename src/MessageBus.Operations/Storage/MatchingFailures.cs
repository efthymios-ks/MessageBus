namespace MessageBus.Operations.Storage;

/// <summary>Preview of what a bulk action will touch. Feeds the confirm dialog.</summary>
/// <param name="Total">Number of failures the filter matches, across every page.</param>
/// <param name="Groups">Per-endpoint counts. One row per receiving endpoint.</param>
public sealed record MatchingFailures(int Total, IReadOnlyList<MatchingFailureGroup> Groups);

/// <summary>One group in a <see cref="MatchingFailures"/> preview.</summary>
/// <param name="EndpointName">Endpoint that failed the messages, and where a retry is sent.</param>
/// <param name="Count">How many failed messages fall into this endpoint.</param>
public sealed record MatchingFailureGroup(string EndpointName, int Count);
