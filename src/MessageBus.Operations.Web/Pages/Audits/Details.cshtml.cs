using System.Text.Json;
using MessageBus.Operations.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Audits;

/// <summary>Details page for a single audited message.</summary>
public sealed class DetailsModel(AuditQuery audits, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>The audited message, or null when none was found.</summary>
    public AuditedMessage? Audit { get; private set; }

    /// <summary>The payload rendered as pretty JSON, or the raw text when it does not parse.</summary>
    public string PayloadJson { get; private set; } = "(no payload)";

    /// <summary>The transport headers as ordered key/value pairs.</summary>
    public IReadOnlyList<KeyValuePair<string, string?>> Headers { get; private set; } = [];

    /// <summary>Loads the audited message identified by <paramref name="messageId"/>, or 404s when missing.</summary>
    public async Task<IActionResult> OnGetAsync([FromRoute(Name = "id")] string messageId, CancellationToken cancellationToken)
    {
        Audit = await audits.FindAsync(messageId, cancellationToken);
        if (Audit is null)
        {
            return NotFound();
        }

        Headers = ParseHeaders(Audit.Headers);
        PayloadJson = FormatPayload(Audit.Payload);
        MarkRendered(timeProvider);

        return Page();
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> ParseHeaders(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed is null) return [];

            return
            [
                .. parsed.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value))
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatPayload(byte[] payload)
    {
        if (payload is null or { Length: 0 }) return "(no payload)";

        var text = System.Text.Encoding.UTF8.GetString(payload);

        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return text;
        }
    }
}
