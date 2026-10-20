using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Pops;

public sealed class PopsIndexModel(PopService pops, CurrentUser current) : PageModel
{
    public List<Pop> Pops { get; private set; } = [];
    public new UserAccount User => current.Required;

    public async Task OnGetAsync() => Pops = await pops.ListByAuthorAsync(User.Id);
}
