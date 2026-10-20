using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Admin;

public sealed class AdminPopsModel(PopService pops) : PageModel
{
    public List<Pop> Pops { get; private set; } = [];
    public string Filter { get; private set; } = "aguardando";

    public async Task OnGetAsync(string? situacao)
    {
        Filter = situacao is "devolvidos" or "validados" or "rascunhos" or "todos" ? situacao : "aguardando";
        Pops = await pops.ListAsync(Filter switch
        {
            "devolvidos" => PopStatus.Devolvido,
            "validados" => PopStatus.Validado,
            "rascunhos" => PopStatus.Rascunho,
            "todos" => null,
            _ => PopStatus.Enviado
        });
    }
}
