using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages;

/// <summary>Sends each profile to its starting page.</summary>
public sealed class IndexModel(CurrentUser current) : PageModel
{
    public IActionResult OnGet() => Redirect(current.Required.IsAdmin ? "/admin/pops" : "/pops");
}
