namespace MessageBus.Explorer;

/// <summary>
/// One message this endpoint handles. <paramref name="HandlerNames"/> is shown because an event
/// with three handlers behaves differently from one with none, and the page is the only place that
/// is visible without reading the code.
/// </summary>
internal sealed record ExplorableMessage(
    string WireName,
    string ClrTypeName,
    string Kind,
    string SampleJson,
    IReadOnlyList<string> HandlerNames
);
