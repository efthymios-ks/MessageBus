using System.Text.Json;
using MessageBus.Operations.Actions;
using MessageBus.Operations.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Errors;

/// <summary>Details page for a single failed message with delete and edit-and-retry actions.</summary>
public sealed class DetailsModel(FailureQuery failures, FailureActionService actions, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>The failure being viewed, or null when none was found.</summary>
    public FailedMessage? Failure { get; private set; }

    /// <summary>The payload rendered as pretty JSON, or the raw text when it does not parse.</summary>
    public string PayloadJson { get; private set; } = "(no payload)";

    /// <summary>The transport headers as ordered key/value pairs.</summary>
    public IReadOnlyList<KeyValuePair<string, string?>> Headers { get; private set; } = [];

    /// <summary>Loads the failure identified by <paramref name="messageId"/>, or 404s when missing.</summary>
    public async Task<IActionResult> OnGetAsync([FromRoute(Name = "id")] string messageId, CancellationToken cancellationToken)
    {
        Failure = await failures.FindAsync(messageId, cancellationToken);
        if (Failure is null)
        {
            return NotFound();
        }

        Headers = ParseHeaders(Failure.Headers);
        PayloadJson = FormatPayload(Failure.Payload);
        MarkRendered(timeProvider);

        return Page();
    }

    /// <summary>Deletes the failure identified by <paramref name="messageId"/> and returns to the list.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(
        [FromRoute(Name = "id")] string messageId,
        [FromForm] string? reason,
        CancellationToken cancellationToken
    )
    {
        var count = await actions.DeleteAsync([messageId], Actor, reason ?? "(no reason given)", cancellationToken);
        StatusMessage = count == 1 ? "Message deleted." : "The message was not found.";

        return RedirectToPage("/Errors/Index");
    }

    /// <summary>Dispatches a corrected copy of the failed message with edited body and headers.</summary>
    public async Task<IActionResult> OnPostEditAndRetryAsync(
        [FromRoute(Name = "id")] string messageId,
        [FromForm] string payload,
        [FromForm] string? destination,
        [FromForm] string? contentType,
        [FromForm] string? correlationId,
        [FromForm] string? causationId,
        [FromForm] string? reason,
        CancellationToken cancellationToken
    )
    {
        var failure = await failures.FindAsync(messageId, cancellationToken);
        if (failure is null)
        {
            return NotFound();
        }

        var editedHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(destination)) editedHeaders["destination"] = destination;
        if (!string.IsNullOrWhiteSpace(contentType)) editedHeaders["content-type"] = contentType;
        if (!string.IsNullOrWhiteSpace(correlationId)) editedHeaders["correlation-id"] = correlationId;
        if (!string.IsNullOrWhiteSpace(causationId)) editedHeaders["causation-id"] = causationId;

        await actions.EditAndRetryAsync(messageId, payload, editedHeaders, Actor, reason ?? "(no reason given)", cancellationToken);
        StatusMessage = "Corrected copy dispatched.";

        return RedirectToPage("/Errors/Index");
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
