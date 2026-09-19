namespace MessageBus.Operations.Storage;

/// <summary>
/// An endpoint Operations expects to hear from. Created by registering an API key, which is what
/// makes silence alertable: without a registration, an endpoint that never starts is
/// indistinguishable from one that was never deployed.
/// </summary>
public sealed class EndpointRegistration
{
    /// <summary>Logical name of the endpoint. Primary key.</summary>
    public required string EndpointName { get; set; }

    /// <summary>Plaintext key the endpoint presents on the heartbeats endpoint.</summary>
    public required string ApiKey { get; set; }

    /// <summary>When the registration was created.</summary>
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>
    /// How long silence is tolerated before the endpoint reads as stale. Per endpoint, because a
    /// batch worker and an API do not report at the same rhythm.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A disabled endpoint's key is refused at <c>/api/heartbeats</c> and it disappears from the
    /// runtime views. The row is kept so a rename or re-enable does not lose history, and the
    /// audits and failures it produced continue to reference a name that still exists.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>Instances that reported under this registration.</summary>
    public List<EndpointInstance> Instances { get; } = [];
}
