namespace MessageBus.Operations.Client;

/// <summary>What an endpoint needs to report to Operations.</summary>
public sealed class OperationsClientOptions
{
    /// <summary>Name used to look up the client from <see cref="System.Net.Http.IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "messagebus-operations";

    /// <summary>Where Operations is. Without it the endpoint simply does not report.</summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>
    /// Issued by Operations when the endpoint was registered. It is the endpoint's identity: the
    /// name in the body is checked against it rather than trusted.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Defaults to a random id per process, which is what an instance is.</summary>
    public string? InstanceId { get; set; }

    /// <summary>Deployed assembly version, reported to Operations so drift is visible.</summary>
    public string? Version { get; set; }

    /// <summary>How often to send a heartbeat. Overridden by whatever Operations echoes back.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Overrides <see cref="BaseAddress"/>. Returns this instance for chaining.</summary>
    public OperationsClientOptions WithBaseAddress(string baseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        BaseAddress = baseAddress;

        return this;
    }

    /// <summary>Overrides <see cref="ApiKey"/>. Returns this instance for chaining.</summary>
    public OperationsClientOptions WithApiKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        ApiKey = apiKey;

        return this;
    }
}
