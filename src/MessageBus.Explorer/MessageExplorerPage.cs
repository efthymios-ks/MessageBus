using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace MessageBus.Explorer;

/// <summary>
/// The whole page, filled from embedded HTML templates. No Razor and no script: it is a list of
/// textareas and submit buttons, and a view engine in a library would be a dependency every host
/// inherits for that.
/// </summary>
internal static class MessageExplorerPage
{
    private static readonly string _pageTemplate = LoadTemplate("MessageExplorerPage.html");

    private static readonly string _sectionTemplate = LoadTemplate("MessageExplorerSection.html");

    public static string Render(
        string endpointName,
        string routePrefix,
        IReadOnlyList<ExplorableMessage> messages,
        string? statusMessage,
        string? errorMessage,
        string? preservedWireName = null,
        string? preservedBody = null
    )
    {
        return _pageTemplate
            .Replace("{{EndpointName}}", Encode(endpointName))
            .Replace("{{Notices}}", BuildNotices(statusMessage, errorMessage))
            .Replace("{{Sections}}", BuildSections(messages, routePrefix, preservedWireName, preservedBody));
    }

    private static string BuildNotices(string? statusMessage, string? errorMessage)
    {
        var notices = new StringBuilder();

        if (statusMessage is { Length: > 0 })
        {
            notices.Append($"<div class=\"banner success\">{Encode(statusMessage)}</div>");
        }

        if (errorMessage is { Length: > 0 })
        {
            notices.Append($"<div class=\"banner error\">{Encode(errorMessage)}</div>");
        }

        return notices.ToString();
    }

    private static string BuildSections(
        IReadOnlyList<ExplorableMessage> messages,
        string routePrefix,
        string? preservedWireName,
        string? preservedBody
    )
    {
        if (messages.Count == 0)
        {
            return "<div class=\"empty\">This endpoint handles nothing. It only sends.</div>";
        }

        var sections = new StringBuilder();

        foreach (var message in messages)
        {
            // Keep whatever the operator just sent, so the textarea does not reset after Send.
            var body = string.Equals(message.WireName, preservedWireName, StringComparison.Ordinal)
                && preservedBody is { Length: > 0 }
                ? preservedBody
                : message.SampleJson;

            sections.Append(_sectionTemplate
                .Replace("{{WireName}}", Encode(message.WireName))
                .Replace("{{Kind}}", Encode(message.Kind))
                .Replace("{{KindLower}}", Encode(message.Kind.ToLowerInvariant()))
                .Replace("{{HandlerNames}}", Encode(string.Join(", ", message.HandlerNames)))
                .Replace("{{RoutePrefix}}", Encode(routePrefix))
                .Replace("{{SampleJson}}", Encode(body))
                .Replace("{{Rows}}", Rows(body).ToString(CultureInfo.InvariantCulture)));
        }

        return sections.ToString();
    }

    /// <summary>Sized to the sample, so a one-field message does not get a sixteen-line box.</summary>
    private static int Rows(string sampleJson)
        => Math.Clamp(sampleJson.Count(character => character == '\n') + 2, 4, 24);

    private static string Encode(string? value)
        => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private static string LoadTemplate(string name)
    {
        var resourceName = $"{typeof(MessageExplorerPage).Namespace}.{name}";
        var assembly = typeof(MessageExplorerPage).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
