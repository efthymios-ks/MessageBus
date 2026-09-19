using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MessageBus.Operations.Web.Pages;

/// <summary>Root page. Redirects to the Endpoints screen so operators land on live state.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Redirects to <c>/Endpoints/Index</c>.</summary>
    public IActionResult OnGet()
        => RedirectToPage("/Endpoints/Index");
}
