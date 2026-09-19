using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MessageBus.Transport.RabbitMq;

/// <summary>
/// Checks that each event exchange is bound to this endpoint's queue. AMQP has no passive binding
/// declare, so this is the management API or nothing — and nothing means a queue bound to the wrong
/// exchange passes startup and shows up later as an event that silently never arrives.
/// </summary>
internal sealed class RabbitMqBindingVerifier(RabbitMqOptions options) : IDisposable
{
    // The management API answers in snake_case; without this every field reads as null and every
    // binding looks missing.
    private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient = CreateHttpClient(options);

    public async Task<IReadOnlyList<string>> MissingBindingsAsync(
        string queueName,
        IReadOnlyList<string> exchangeNames,
        CancellationToken cancellationToken
    )
    {
        if (exchangeNames.Count == 0)
        {
            return [];
        }

        var bindings = await ReadBindingsAsync(queueName, cancellationToken);

        return
        [
            .. exchangeNames
                .Where(exchangeName => !bindings.Contains(exchangeName))
                .Select(exchangeName => $"binding from exchange '{exchangeName}' to queue '{queueName}'")
        ];
    }

    public void Dispose()
        => _httpClient.Dispose();

    private async Task<HashSet<string>> ReadBindingsAsync(string queueName, CancellationToken cancellationToken)
    {
        var virtualHost = Uri.EscapeDataString(options.VirtualHost);
        var path = $"api/queues/{virtualHost}/{Uri.EscapeDataString(queueName)}/bindings";

        using var response = await _httpClient.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // The queue check already reported this, and saying it twice helps nobody.
            return [];
        }

        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);

        var bindings = await JsonSerializer.DeserializeAsync<List<RabbitMqBindingResponse>>(
            content,
            _serializerOptions,
            cancellationToken
        );

        // The default exchange binds every queue to itself under an empty source name, which is how
        // a command reaches its queue and is not a binding anyone declared.
        return bindings is null
            ? []
            : [.. bindings
                .Select(binding => binding.Source)
                .Where(source => !string.IsNullOrEmpty(source))];
    }

    private static HttpClient CreateHttpClient(RabbitMqOptions options)
    {
        var managementUri = new Uri(options.ManagementUrl!.TrimEnd('/') + "/");
        var httpClient = new HttpClient { BaseAddress = managementUri, Timeout = TimeSpan.FromSeconds(10) };

        // Taken from the AMQP URI rather than configured twice: they are the same broker and the same
        // account, and two places to put a password is one place to get it wrong.
        var credentials = CredentialsFrom(options.ConnectionString);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))
        );

        return httpClient;
    }

    private static string CredentialsFrom(string connectionString)
    {
        var userInfo = new Uri(connectionString).UserInfo;

        return string.IsNullOrEmpty(userInfo)
            ? "guest:guest"
            : Uri.UnescapeDataString(userInfo);
    }
}
