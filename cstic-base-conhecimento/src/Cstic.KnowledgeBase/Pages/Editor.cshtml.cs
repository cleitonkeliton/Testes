using System.Text.Json;
using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages;

public sealed class EditorModel(DocumentService documents, DocumentImportService importer) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12
    };

    [BindProperty]
    public string? Payload { get; set; }
    [BindProperty]
    public IFormFile? UploadedFile { get; set; }
    public bool IsReview => RouteData.Values["mode"]?.ToString() == "validar";
    public string CreationDate => documents.CreationDate;

    public void OnGet() => Response.Headers.CacheControl = "no-store";

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (!IsReview) return NotFound();
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
            return new JsonResult(new { imported.Article, imported.OriginalDate, imported.Warnings,
                fileName = Path.GetFileName(UploadedFile.FileName.Replace('\\', '/')) });
        }
        catch (DocumentImportException exception) { return Invalid(exception.Message); }
    }

    public IActionResult OnPostGenerate()
    {
        if (string.IsNullOrWhiteSpace(Payload) || Payload.Length > 180_000)
            return Invalid("O formulário está vazio ou excede o limite de tamanho.");
        KnowledgeArticle? article;
        try { article = JsonSerializer.Deserialize<KnowledgeArticle>(Payload, JsonOptions); }
        catch (JsonException) { return Invalid("Não foi possível ler o formulário. Recarregue a página e tente novamente."); }
        if (article is null) return Invalid("Preencha o formulário antes de gerar o documento.");
        var errors = ArticleValidator.Validate(article);
        if (errors.Count > 0)
            return new JsonResult(new { message = "Revise os campos indicados antes de gerar o documento.", errors })
                { StatusCode = StatusCodes.Status400BadRequest };
        var word = documents.CreateWord(article);
        return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DocumentService.FileName(article));
    }

    private static JsonResult Invalid(string message) => new(new { message })
        { StatusCode = StatusCodes.Status400BadRequest };
}
