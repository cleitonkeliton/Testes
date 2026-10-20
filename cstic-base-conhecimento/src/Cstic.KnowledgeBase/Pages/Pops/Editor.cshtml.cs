using System.Text.Json;
using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Pops;

public sealed class EditorModel(DocumentService documents, DocumentImportService importer, PopService pops,
    CurrentUser current) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12
    };

    [BindProperty]
    public string? Payload { get; set; }
    [BindProperty]
    public IFormFile? UploadedFile { get; set; }
    public Pop? Pop { get; private set; }
    /// <summary>Saved content for the form; the default encoder escapes &lt; so it is safe inside a script element.</summary>
    public string InitialJson { get; private set; } = "null";
    public bool IsImport => RouteData.Values["mode"]?.ToString() == "importar";
    public string CreationDate => documents.CreationDate;
    public string AuthorName => Pop?.Author.FullName ?? current.Required.FullName;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Response.Headers.CacheControl = "no-store";
        if (id is null) return Page();
        Pop = await pops.FindAsync(id.Value);
        if (Pop is null || !PopService.CanView(current.Required, Pop)) return NotFound();
        if (!PopService.CanEdit(current.Required, Pop)) return Redirect($"/pops/{Pop.Id}");
        InitialJson = JsonSerializer.Serialize(PopService.Content(Pop.ContentJson), JsonOptions);
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (!IsImport) return NotFound();
        if (UploadedFile is null || UploadedFile.Length == 0)
            return Invalid("Selecione o arquivo Word que deseja validar.");
        if (!string.Equals(Path.GetExtension(UploadedFile.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
            return Invalid("Envie um arquivo .docx no modelo CSTIC. PDF e .doc não são aceitos.");
        if (UploadedFile.Length > DocumentImportService.MaxFileBytes)
            return Invalid("O arquivo deve ter no máximo 2 MB.");
        using var memory = new MemoryStream();
        await UploadedFile.CopyToAsync(memory, HttpContext.RequestAborted);
        try
        {
            var imported = importer.Import(memory.ToArray());
            var fileName = Path.GetFileName(UploadedFile.FileName.Replace('\\', '/'));
            var pop = await pops.ImportAsync(current.Required, imported, fileName);
            var date = imported.OriginalDate.Length > 0 ? $" Data no arquivo: {imported.OriginalDate}." : "";
            TempData["Notice"] = $"Arquivo carregado: {fileName}.{date} Confira todos os campos, salve e envie para validação. {string.Join(" ", imported.Warnings)}".Trim();
            return new JsonResult(new { redirect = $"/pops/{pop.Id}/editar" });
        }
        catch (DocumentImportException exception) { return Invalid(exception.Message); }
    }

    public async Task<IActionResult> OnPostSaveAsync(int? id)
    {
        if (ReadPayload(out var article) is { } invalid) return invalid;
        try
        {
            var pop = await pops.SaveAsync(current.Required, id, article!);
            return new JsonResult(new { id = pop.Id, message = $"Rascunho salvo às {Dates.Format(pop.UpdatedUtc)[^5..]}." });
        }
        catch (PopException exception) { return Invalid(exception.Message); }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int? id)
    {
        if (ReadPayload(out var article) is { } invalid) return invalid;
        try
        {
            var (pop, errors) = await pops.SubmitAsync(current.Required, id, article!);
            if (errors.Count > 0)
                return new JsonResult(new { id = pop.Id, message = "O rascunho foi salvo, mas faltam informações. Revise os campos indicados antes de enviar.", errors })
                    { StatusCode = StatusCodes.Status400BadRequest };
            TempData["Notice"] = "POP enviado para validação. Você será informado aqui se houver ajustes.";
            return new JsonResult(new { id = pop.Id, redirect = $"/pops/{pop.Id}" });
        }
        catch (PopException exception) { return Invalid(exception.Message); }
    }

    /// <summary>Word file of the content in the form, with the header the POP has before validation.</summary>
    public async Task<IActionResult> OnPostDownloadAsync(int? id)
    {
        if (ReadPayload(out var article) is { } invalid) return invalid;
        var pop = id is null ? new Pop { AuthorId = current.Required.Id, Author = current.Required } : await pops.FindAsync(id.Value);
        if (pop is null || !PopService.CanView(current.Required, pop)) return NotFound();
        PopService.Document(pop, article!);
        var errors = ArticleValidator.Validate(article!);
        if (errors.Count > 0)
            return new JsonResult(new { message = "Revise os campos indicados antes de gerar o documento.", errors })
                { StatusCode = StatusCodes.Status400BadRequest };
        return File(documents.CreateWord(article!), "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DocumentService.FileName(article!));
    }

    private IActionResult? ReadPayload(out KnowledgeArticle? article)
    {
        article = null;
        if (string.IsNullOrWhiteSpace(Payload))
            return Invalid("Preencha o formulário antes de salvar.");
        if (Payload.Length > ArticleValidator.MaxPayloadLength)
            return Invalid(ArticleValidator.PayloadTooLongMessage);
        try { article = JsonSerializer.Deserialize<KnowledgeArticle>(Payload, JsonOptions); }
        catch (JsonException) { return Invalid("Não foi possível ler o formulário. Recarregue a página e tente novamente."); }
        return article is null ? Invalid("Preencha o formulário antes de salvar.") : null;
    }

    private static JsonResult Invalid(string message) => new(new { message })
        { StatusCode = StatusCodes.Status400BadRequest };
}
