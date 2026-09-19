using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MessageBus.Operations.Web.Pages;

/// <summary>
/// What every page here shares: who is acting, and how to end a POST. Post-redirect-get is not a
/// nicety — a refresh that re-posts a retry batch is a real duplicate, and the browser will offer
/// to do it.
/// </summary>
public abstract class OperationsPageModel : PageModel
{
    /// <summary>
    /// The name recorded against every action. Authentication is an open decision, so this falls
    /// back to a placeholder rather than pretending an anonymous action had an author.
    /// </summary>
    protected string Actor
        => User.Identity?.Name is { Length: > 0 } name ? name : "anonymous";

    /// <summary>Shown once after a redirect, which is where the outcome of an action belongs.</summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>Shown once after a redirect when an action failed.</summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>Data arrives eventually. Pages say when they were rendered rather than implying live state.</summary>
    public DateTimeOffset RenderedAt { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Sets <see cref="RenderedAt"/> to the current time. Called by pages at the end of a load.</summary>
    protected void MarkRendered(TimeProvider timeProvider)
        => RenderedAt = timeProvider.GetUtcNow();

    /// <summary>UTC datetime rendered as <c>YYYY-MM-DD HH:MM:SSZ</c> — the shape used everywhere in the UI.</summary>
    public static string FormatUtc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "Z";

    /// <summary>UTC datetime rendered like <see cref="FormatUtc(DateTimeOffset)"/>, or an em dash when null.</summary>
    public static string FormatUtc(DateTimeOffset? value)
        => value is { } dt ? FormatUtc(dt) : "—";
}
