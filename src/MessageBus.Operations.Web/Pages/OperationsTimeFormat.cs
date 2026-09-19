using System.Globalization;

namespace MessageBus.Operations.Web.Pages;

/// <summary>
/// Time formatting shared across pages, view components and partials.
/// Absolute always ISO-style UTC. Relative always the two-coarsest-non-zero-units shape.
/// </summary>
public static class OperationsTimeFormat
{
    /// <summary>UTC datetime rendered as <c>yyyy-MM-dd HH:mm:ssZ</c>.</summary>
    public static string Absolute(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    /// <summary>Same as <see cref="Absolute(DateTimeOffset)"/>, or an em dash when null.</summary>
    public static string Absolute(DateTimeOffset? value)
        => value is { } dt ? Absolute(dt) : "—";

    /// <summary>
    /// Human-readable relative timespan.
    /// Format: <c>after {coarsest}{mate}</c> where <c>mate</c> is the immediately smaller unit,
    /// included only when non-zero.
    /// <c>1h 0m 10s</c> collapses to <c>after 1h</c>.
    /// Milliseconds round up to 1s so a sub-second hop never vanishes.
    /// </summary>
    public static string Relative(TimeSpan span)
    {
        if (span < TimeSpan.FromSeconds(1))
        {
            span = TimeSpan.FromSeconds(1);
        }

        var days = (int)span.TotalDays;
        var hours = span.Hours;
        var minutes = span.Minutes;
        var seconds = span.Seconds;

        string tail;
        if (days > 0)
        {
            tail = hours > 0 ? $"{days}d {hours}h" : $"{days}d";
        }
        else if (hours > 0)
        {
            tail = minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }
        else if (minutes > 0)
        {
            tail = seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }
        else
        {
            tail = $"{seconds}s";
        }

        return "after " + tail;
    }
}
