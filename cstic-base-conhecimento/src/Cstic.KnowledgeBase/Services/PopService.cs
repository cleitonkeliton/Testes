using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cstic.KnowledgeBase.Data;
using Cstic.KnowledgeBase.Models;
using Microsoft.EntityFrameworkCore;

namespace Cstic.KnowledgeBase.Services;

public sealed class PopException(string message) : Exception(message);

/// <summary>POP lifecycle: draft → sent → returned/validated, with automatic number and version.</summary>
public sealed class PopService(AppDbContext db, TimeProvider clock, DocumentService documents)
{
    public const string PendingCode = "PENDENTE";
    public const string PendingApprover = "Pendente de validação";
    private static readonly Regex CodeFormat = new(@"^[A-Za-z0-9][A-Za-z0-9._/-]{0,39}$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionFormat = new(@"^\d{1,3}(\.\d{1,3}){0,2}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public static string Serialize(KnowledgeArticle article) => JsonSerializer.Serialize(article, Json);

    public static KnowledgeArticle Content(string json)
    {
        var article = JsonSerializer.Deserialize<KnowledgeArticle>(json, Json) ?? new KnowledgeArticle();
        article.Actions = (article.Actions ?? []).Where(action => action is not null).ToList();
        foreach (var action in article.Actions)
        {
            action.Steps = (action.Steps ?? []).Where(step => step is not null).ToList();
            foreach (var step in action.Steps) step.Details = (step.Details ?? []).Where(detail => detail is not null).ToList();
        }
        return article;
    }

    /// <summary>1.0 for the first validation, then 1.1, 1.2… (the minor number grows on each revision).</summary>
    public static string NextVersion(string? current)
    {
        if (current is null) return "1.0";
        var parts = current.Split('.');
        var major = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minor = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        return $"{major}.{minor + 1}";
    }

    public Task<List<Pop>> ListByAuthorAsync(int userId) => db.Pops.Where(pop => pop.AuthorId == userId)
        .OrderByDescending(pop => pop.UpdatedUtc).ToListAsync();

    public Task<List<Pop>> ListAsync(PopStatus? status)
    {
        var query = db.Pops.Include(pop => pop.Author).AsQueryable();
        if (status is not null) query = query.Where(pop => pop.Status == status);
        return status == PopStatus.Enviado
            ? query.OrderBy(pop => pop.SubmittedUtc).ToListAsync()
            : query.OrderByDescending(pop => pop.UpdatedUtc).ToListAsync();
    }

    public Task<int> WaitingCountAsync() => db.Pops.CountAsync(pop => pop.Status == PopStatus.Enviado);

    public Task<Pop?> FindAsync(int id) => db.Pops
        .Include(pop => pop.Author)
        .Include(pop => pop.Events.OrderBy(item => item.CreatedUtc)).ThenInclude(item => item.User)
        .Include(pop => pop.Versions.OrderByDescending(item => item.Id)).ThenInclude(item => item.ValidatedBy)
        .AsSplitQuery()
        .SingleOrDefaultAsync(pop => pop.Id == id);

    public static bool CanView(UserAccount user, Pop pop) => user.IsAdmin || pop.AuthorId == user.Id;
    public static bool CanEdit(UserAccount user, Pop pop) => pop.AuthorId == user.Id && pop.IsEditable;

    public async Task<Pop> SaveAsync(UserAccount user, int? popId, KnowledgeArticle content)
    {
        Pop pop;
        if (popId is null)
        {
            pop = new Pop { AuthorId = user.Id, Author = user, CreatedUtc = Now };
            db.Pops.Add(pop);
            AddEvent(pop, user, PopEventType.Criado);
        }
        else
        {
            pop = await FindAsync(popId.Value) ?? throw new PopException("POP não encontrado.");
            if (!CanEdit(user, pop))
                throw new PopException(pop.Status == PopStatus.Enviado
                    ? "Este POP está aguardando validação e não pode ser alterado agora."
                    : "Você não pode alterar este POP.");
            if (pop.Status == PopStatus.Validado)
            {
                pop.Status = PopStatus.Rascunho;
                AddEvent(pop, user, PopEventType.RevisaoIniciada);
            }
        }
        pop.Title = Truncate(content.Title?.Trim() ?? "", 160);
        pop.ContentJson = Serialize(Content(Serialize(content)));
        pop.UpdatedUtc = Now;
        await db.SaveChangesAsync();
        return pop;
    }

    /// <summary>Keeps the number and version of the original file, when valid and not used by another POP.</summary>
    public async Task<Pop> ImportAsync(UserAccount user, ImportedArticle imported, string fileName)
    {
        var article = imported.Article;
        var code = article.Code.Trim();
        var pop = new Pop
        {
            AuthorId = user.Id, Author = user, CreatedUtc = Now, UpdatedUtc = Now,
            Title = Truncate(article.Title.Trim(), 160), ContentJson = Serialize(article),
            Code = CodeFormat.IsMatch(code) && !code.Equals(PendingCode, StringComparison.OrdinalIgnoreCase) &&
                   !await db.Pops.AnyAsync(item => item.Code == code) ? code : null,
            Version = VersionFormat.IsMatch(article.Version.Trim()) ? article.Version.Trim() : null
        };
        db.Pops.Add(pop);
        AddEvent(pop, user, PopEventType.Importado, Truncate($"Arquivo {fileName}", 300));
        await db.SaveChangesAsync();
        return pop;
    }

    /// <summary>Saves and sends for validation; returns field errors when the content is incomplete.</summary>
    public async Task<(Pop Pop, Dictionary<string, string[]> Errors)> SubmitAsync(UserAccount user, int? popId, KnowledgeArticle content)
    {
        var pop = await SaveAsync(user, popId, content);
        var errors = ArticleValidator.Validate(Document(pop));
        if (errors.Count > 0) return (pop, errors);
        pop.Status = PopStatus.Enviado;
        pop.SubmittedUtc = Now;
        AddEvent(pop, user, PopEventType.Enviado);
        await db.SaveChangesAsync();
        return (pop, errors);
    }

    public async Task ReturnAsync(UserAccount admin, int popId, string? comment)
    {
        var pop = await WaitingAsync(popId);
        var text = comment?.Trim() ?? "";
        if (text.Length < 5 || text.Length > 2000)
            throw new PopException("Descreva o que precisa ser ajustado (de 5 a 2.000 caracteres).");
        pop.Status = PopStatus.Devolvido;
        pop.ReturnComment = text;
        pop.UpdatedUtc = Now;
        AddEvent(pop, admin, PopEventType.Devolvido, text);
        await db.SaveChangesAsync();
    }

    public async Task<PopVersion> ValidateAsync(UserAccount admin, int popId)
    {
        var pop = await WaitingAsync(popId);
        if (pop.Code is null)
        {
            var codes = await db.Pops.Where(item => item.Code != null).Select(item => item.Code!).ToListAsync();
            var last = codes.Select(code => int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
                .DefaultIfEmpty(0).Max();
            pop.Code = (last + 1).ToString("D4", CultureInfo.InvariantCulture);
        }
        pop.Version = NextVersion(pop.Version);
        var article = Content(pop.ContentJson);
        article.Code = pop.Code;
        article.Version = pop.Version;
        article.Author = pop.Author.FullName;
        article.Approver = admin.FullName;
        if (ArticleValidator.Validate(article).Count > 0)
            throw new PopException("O conteúdo deste POP está incompleto. Devolva-o ao técnico para correção.");
        var version = new PopVersion
        {
            PopId = pop.Id, Code = pop.Code, Version = pop.Version, ContentJson = Serialize(article),
            ValidatedById = admin.Id, ValidatedBy = admin, ValidatedUtc = Now
        };
        pop.Versions.Add(version);
        pop.Status = PopStatus.Validado;
        pop.ReturnComment = null;
        pop.UpdatedUtc = Now;
        AddEvent(pop, admin, PopEventType.Validado, $"Nº {pop.Code}, versão {pop.Version}");
        await db.SaveChangesAsync();
        return version;
    }

    /// <summary>Current content with the header it will have: number and version stay pending until validated.</summary>
    public static KnowledgeArticle Document(Pop pop)
    {
        var article = Content(pop.ContentJson);
        article.Code = pop.Code ?? PendingCode;
        article.Version = NextVersion(pop.Version);
        article.Author = pop.Author.FullName;
        article.Approver = PendingApprover;
        return article;
    }

    public static KnowledgeArticle Document(Pop pop, KnowledgeArticle content)
    {
        var header = Document(pop);
        content.Code = header.Code;
        content.Version = header.Version;
        content.Author = header.Author;
        content.Approver = header.Approver;
        return content;
    }

    /// <summary>The validated file (latest or requested version), or the current draft when there is none.</summary>
    public (byte[] Bytes, string FileName) Download(Pop pop, string? version = null)
    {
        var validated = version is null
            ? (pop.Status == PopStatus.Validado ? pop.Versions.MaxBy(item => item.Id) : null)
            : pop.Versions.SingleOrDefault(item => item.Version == version) ?? throw new PopException("Versão não encontrada.");
        if (validated is not null)
        {
            var article = Content(validated.ContentJson);
            return (documents.CreateWord(article, new DateTimeOffset(validated.ValidatedUtc, TimeSpan.Zero)), DocumentService.FileName(article));
        }
        var draft = Document(pop);
        if (ArticleValidator.Validate(draft).Count > 0)
            throw new PopException("Preencha todos os campos obrigatórios antes de baixar o Word.");
        return (documents.CreateWord(draft), DocumentService.FileName(draft));
    }

    private async Task<Pop> WaitingAsync(int popId)
    {
        var pop = await FindAsync(popId) ?? throw new PopException("POP não encontrado.");
        if (pop.Status != PopStatus.Enviado) throw new PopException("Este POP não está aguardando validação.");
        return pop;
    }

    private void AddEvent(Pop pop, UserAccount user, PopEventType type, string? comment = null) =>
        pop.Events.Add(new PopEvent { UserId = user.Id, User = user, Type = type, Comment = comment, CreatedUtc = Now });

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
