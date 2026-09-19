using System.Globalization;
using MessageBus.Operations.Heartbeats;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Keys;

/// <summary>The Keys screen: manage endpoint registrations and their API keys.</summary>
public sealed class IndexModel(EndpointKeyService keys, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>The list backing the table.</summary>
    public IReadOnlyList<EndpointKeyView> Keys { get; private set; } = [];

    /// <summary>When true, disabled rows are included in the list.</summary>
    [BindProperty(SupportsGet = true, Name = "showDisabled")]
    public bool ShowDisabled { get; set; }

    /// <summary>Set after Create so the plaintext value shows once in the banner.</summary>
    [TempData]
    public string? NewKeyValue { get; set; }

    /// <summary>Endpoint whose plaintext key is shown once in the banner after Create.</summary>
    [TempData]
    public string? NewKeyEndpoint { get; set; }

    /// <summary>"create" or "edit" — which modal to re-open on load, populated with the failed input.</summary>
    public string? OpenModal { get; private set; }

    /// <summary>Values to prefill the Create modal with, populated on validation failure.</summary>
    public CreateFormValues CreateForm { get; private set; } = CreateFormValues.Empty;

    /// <summary>Validation error shown inside the Create modal, or null when there is none.</summary>
    public string? CreateError { get; private set; }

    /// <summary>Values to prefill the Edit modal with, populated on validation failure.</summary>
    public EditFormValues EditForm { get; private set; } = EditFormValues.Empty;

    /// <summary>Validation error shown inside the Edit modal, or null when there is none.</summary>
    public string? EditError { get; private set; }

    /// <summary>Loads the key list.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadListAsync(cancellationToken);
    }

    /// <summary>Creates an endpoint registration with the given key and stale-after window.</summary>
    public async Task<IActionResult> OnPostCreateAsync(
        [FromForm] string? endpointName,
        [FromForm] string? key,
        [FromForm] string? staleAfter,
        CancellationToken cancellationToken
    )
    {
        var name = (endpointName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return await FailCreateAsync(name, key, staleAfter, "Endpoint name is required.", cancellationToken);
        }

        if (!TryParseStale(staleAfter, out var stale, out var staleError))
        {
            return await FailCreateAsync(name, key, staleAfter, staleError, cancellationToken);
        }

        try
        {
            var plaintext = await keys.CreateAsync(name, key, stale, cancellationToken);
            NewKeyEndpoint = name;
            NewKeyValue = plaintext;
            StatusMessage = $"Created endpoint {name}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return await FailCreateAsync(name, key, staleAfter, exception.Message, cancellationToken);
        }

        return RedirectToPage(new { showDisabled = ShowDisabled });
    }

    /// <summary>Saves the edited key, stale-after window and disabled flag for an endpoint.</summary>
    public async Task<IActionResult> OnPostSaveAsync(
        [FromForm] string? endpointName,
        [FromForm] string? key,
        [FromForm] string? staleAfter,
        [FromForm] bool disabled,
        CancellationToken cancellationToken
    )
    {
        var name = (endpointName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return await FailSaveAsync(name, key, staleAfter, disabled, "Endpoint name is required.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return await FailSaveAsync(name, key, staleAfter, disabled, "Key cannot be blank. Use Rotate to generate a random one.", cancellationToken);
        }

        if (!TryParseStale(staleAfter, out var stale, out var staleError))
        {
            return await FailSaveAsync(name, key, staleAfter, disabled, staleError, cancellationToken);
        }

        try
        {
            await keys.SaveAsync(name, key, stale, disabled, cancellationToken);
            StatusMessage = $"Saved {name}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return await FailSaveAsync(name, key, staleAfter, disabled, exception.Message, cancellationToken);
        }

        return RedirectToPage(new { showDisabled = ShowDisabled });
    }

    private async Task<IActionResult> FailCreateAsync(
        string endpointName,
        string? key,
        string? staleAfter,
        string message,
        CancellationToken cancellationToken
    )
    {
        await LoadListAsync(cancellationToken);
        CreateForm = new CreateFormValues(endpointName, key ?? string.Empty, staleAfter ?? string.Empty);
        CreateError = message;
        OpenModal = "create";
        return Page();
    }

    private async Task<IActionResult> FailSaveAsync(
        string endpointName,
        string? key,
        string? staleAfter,
        bool disabled,
        string message,
        CancellationToken cancellationToken
    )
    {
        await LoadListAsync(cancellationToken);
        EditForm = new EditFormValues(endpointName, key ?? string.Empty, staleAfter ?? string.Empty, disabled);
        EditError = message;
        OpenModal = "edit";
        return Page();
    }

    private async Task LoadListAsync(CancellationToken cancellationToken)
    {
        Keys = await keys.ListAsync(ShowDisabled, cancellationToken);
        MarkRendered(timeProvider);
    }

    /// <summary>
    /// Rejects blanks and anything TimeSpan cannot parse. Blank is not the default here — a form
    /// that silently substitutes a default is a form that lies to the user about what they typed.
    /// </summary>
    private static bool TryParseStale(string? value, out TimeSpan stale, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            stale = default;
            error = "Stale after is required. Use hh:mm:ss (e.g. 00:02:00).";
            return false;
        }

        if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out stale) || stale <= TimeSpan.Zero)
        {
            error = $"'{value}' is not a valid positive timespan. Use hh:mm:ss (e.g. 00:02:00).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>Values echoed back into the Create modal on validation failure.</summary>
    /// <param name="EndpointName">The endpoint name entered by the operator.</param>
    /// <param name="Key">The key entered, blank when the operator asked for a generated one.</param>
    /// <param name="StaleAfter">The stale-after window as an <c>hh:mm:ss</c> string.</param>
    public sealed record CreateFormValues(string EndpointName, string Key, string StaleAfter)
    {
        /// <summary>Blank form with the default stale-after window.</summary>
        public static readonly CreateFormValues Empty = new(string.Empty, string.Empty, "00:02:00");
    }

    /// <summary>Values echoed back into the Edit modal on validation failure.</summary>
    /// <param name="EndpointName">The endpoint name being edited.</param>
    /// <param name="Key">The key entered by the operator.</param>
    /// <param name="StaleAfter">The stale-after window as an <c>hh:mm:ss</c> string.</param>
    /// <param name="Disabled">Whether the endpoint is disabled.</param>
    public sealed record EditFormValues(string EndpointName, string Key, string StaleAfter, bool Disabled)
    {
        /// <summary>Blank form with defaults.</summary>
        public static readonly EditFormValues Empty = new(string.Empty, string.Empty, string.Empty, false);
    }
}
