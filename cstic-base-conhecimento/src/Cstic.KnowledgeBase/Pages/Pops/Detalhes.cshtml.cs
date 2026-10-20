using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Pops;

public sealed class DetalhesModel(PopService pops, CurrentUser current) : PageModel
{
    public Pop Pop { get; private set; } = null!;
    public KnowledgeArticle Document { get; private set; } = null!;
    public bool ShowsValidated { get; private set; }
    public new UserAccount User => current.Required;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (await LoadAsync(id) is { } denied) return denied;
        var validated = Pop.Status == PopStatus.Validado ? Pop.Versions.MaxBy(item => item.Id) : null;
        ShowsValidated = validated is not null;
        Document = validated is null ? PopService.Document(Pop) : PopService.Content(validated.ContentJson);
        return Page();
    }

    public async Task<IActionResult> OnGetDocxAsync(int id, string? versao)
    {
        if (await LoadAsync(id) is { } denied) return denied;
        try
        {
            var (bytes, fileName) = pops.Download(Pop, versao);
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (PopException exception)
        {
            TempData["Error"] = exception.Message;
            return Redirect($"/pops/{id}");
        }
    }

    public async Task<IActionResult> OnPostValidateAsync(int id)
    {
        if (!User.IsAdmin) return Forbid();
        try
        {
            var version = await pops.ValidateAsync(User, id);
            TempData["Notice"] = $"POP validado: Nº {version.Code}, versão {version.Version}.";
        }
        catch (PopException exception) { TempData["Error"] = exception.Message; }
        return Redirect($"/pops/{id}");
    }

    public async Task<IActionResult> OnPostReturnAsync(int id, string? comment)
    {
        if (!User.IsAdmin) return Forbid();
        try
        {
            await pops.ReturnAsync(User, id, comment);
            TempData["Notice"] = "POP devolvido ao técnico com o comentário informado.";
            return Redirect("/admin/pops");
        }
        catch (PopException exception) { TempData["Error"] = exception.Message; }
        return Redirect($"/pops/{id}");
    }

    private async Task<IActionResult?> LoadAsync(int id)
    {
        var pop = await pops.FindAsync(id);
        if (pop is null || !PopService.CanView(User, pop)) return NotFound();
        Pop = pop;
        return null;
    }
}
