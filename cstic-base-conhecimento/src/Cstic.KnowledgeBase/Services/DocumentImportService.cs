using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Cstic.KnowledgeBase.Models;

namespace Cstic.KnowledgeBase.Services;

public sealed record ImportedArticle(KnowledgeArticle Article, string OriginalDate, List<string> Warnings);
public sealed class DocumentImportException(string message) : Exception(message);

/// <summary>Reads text from the CSTIC template without extracting or executing uploaded content.</summary>
public sealed class DocumentImportService
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    private const int MaxExpandedBytes = 12 * 1024 * 1024;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Regex ActionNumber = new(@"^\d+\s*[º°ª]\s*[.\-:]?\s*(.+)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex StepNumber = new(@"^\d+\.\d+\s*[.)\-:]?\s+(.+)$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public ImportedArticle Import(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaxFileBytes)
            throw new DocumentImportException("O arquivo deve ter conteúdo e no máximo 2 MB.");
        try { return Read(bytes); }
        catch (DocumentImportException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException or ArgumentException)
        {
            throw new DocumentImportException("Não foi possível ler o documento. Envie um Word .docx válido no modelo CSTIC.");
        }
    }

    private static ImportedArticle Read(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false);
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read);
        if (zip.Entries.Count > 200 || zip.Entries.Any(entry => entry.Length > MaxExpandedBytes) ||
            zip.Entries.Sum(entry => entry.Length) > MaxExpandedBytes ||
            zip.Entries.GroupBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new DocumentImportException("O documento excede os limites de processamento ou possui estrutura inválida.");
        if (zip.Entries.Any(entry => entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
            throw new DocumentImportException("Documentos com macros não são aceitos. Utilize .docx sem macros.");
        var body = Xml(zip, "word/document.xml").Root?.Element(W + "body");
        if (body is null) throw WrongTemplate();
        // Images, changes and nested tables cannot be represented faithfully by the text form.
        if (body.Descendants().Any(node => node.Name.Namespace == W &&
            node.Name.LocalName is "drawing" or "pict" or "object" or "altChunk" or "ins" or "del"))
            throw new DocumentImportException("O arquivo contém imagens, objetos ou alterações controladas. Finalize as alterações e use um POP somente com texto para importar sem perder conteúdo.");
        var tables = body.Elements(W + "tbl").ToArray();
        string[] headings = ["OBJETIVO", "RESULTADO", "REQUISITOS", "EXECUTOR", "PROCEDIMENTO"];
        if (tables.Length != 5 || body.Descendants(W + "tbl").Count() != 5) throw WrongTemplate();
        var cells = new XElement[5];
        for (var index = 0; index < tables.Length; index++)
        {
            var rows = tables[index].Elements(W + "tr").ToArray();
            if (rows.Length != 2 || rows.Any(row => row.Elements(W + "tc").Count() != 1) ||
                !Normalize(CellText(rows[0].Element(W + "tc")!)).EndsWith(headings[index], StringComparison.Ordinal))
                throw WrongTemplate();
            cells[index] = rows[1].Element(W + "tc")!;
        }
        if (body.Elements(W + "p").Any(p => !string.IsNullOrWhiteSpace(ParagraphText(p))))
            throw new DocumentImportException("Há texto fora das cinco seções do modelo. Mova esse conteúdo para a seção correspondente antes de importar.");

        var header = FindHeader(zip, body);
        var metadata = header.Descendants(W + "tbl").Select(table => table.Elements(W + "tr").ToArray())
            .FirstOrDefault(rows => rows.Length == 6 && rows.All(row => row.Elements(W + "tc").Count() == 2));
        if (metadata is null) throw WrongTemplate();
        string[] labels = ["N", "NOME", "VERSAO", "DATACRIACAOATUALIZACAO", "AUTOR", "APROVADOR"];
        var values = new string[6];
        for (var index = 0; index < metadata.Length; index++)
        {
            var pair = metadata[index].Elements(W + "tc").ToArray();
            if (Normalize(CellText(pair[0])) != labels[index]) throw WrongTemplate();
            values[index] = CellText(pair[1]);
        }
        var warnings = new List<string>();
        // Original template hints are not entered data.
        if (values[1] == "(Assunto da base)") values[1] = "";
        if (values[2].StartsWith("1.0 (podendo", StringComparison.Ordinal)) values[2] = "1.0";
        var article = new KnowledgeArticle
        {
            Code = values[0], Title = values[1], Version = values[2], Author = values[4], Approver = values[5],
            Objective = CellText(cells[0]), Result = CellText(cells[1]), Requirements = CellText(cells[2]),
            Executor = CellText(cells[3]), Actions = ParseActions(cells[4], warnings)
        };
        EnsureFits(article);
        if (ArticleValidator.Validate(article).Count > 0)
            warnings.Add("Há campos vazios ou fora do padrão. Confira os dados importados e corrija-os antes de gerar a base revisada.");
        return new(article, values[3], warnings);
    }

    private static XDocument FindHeader(ZipArchive zip, XElement body)
    {
        var references = body.Descendants(W + "headerReference").ToArray();
        var reference = references.FirstOrDefault(node => (string?)node.Attribute(W + "type") == "default")
            ?? references.FirstOrDefault();
        var id = (string?)reference?.Attribute(R + "id");
        if (id is not null)
        {
            var relation = Xml(zip, "word/_rels/document.xml.rels").Root?.Elements()
                .FirstOrDefault(node => (string?)node.Attribute("Id") == id);
            var target = (string?)relation?.Attribute("Target");
            if (target is not null && (string?)relation?.Attribute("TargetMode") != "External")
            {
                var path = target.StartsWith('/') ? target.TrimStart('/') : "word/" + target;
                if (!path.Contains("..", StringComparison.Ordinal) && zip.GetEntry(path) is not null)
                    return Xml(zip, path);
            }
        }
        throw WrongTemplate();
    }

    private static List<ProcedureAction> ParseActions(XElement cell, List<string> warnings)
    {
        var actions = new List<ProcedureAction>();
        ProcedureAction? action = null;
        ProcedureStep? step = null;
        var regrouped = false;
        foreach (var paragraph in cell.Elements(W + "p"))
        {
            var text = ParagraphText(paragraph).Trim();
            if (text.Length == 0) continue;
            var main = ActionNumber.Match(text);
            if (main.Success)
            {
                action = new() { Title = main.Groups[1].Value, Steps = [] };
                actions.Add(action); step = null; continue;
            }
            if (action is null)
            {
                action = new() { Title = "Procedimento importado", Steps = [] };
                actions.Add(action); regrouped = true;
            }
            var sub = StepNumber.Match(text);
            var numbering = paragraph.Element(W + "pPr")?.Element(W + "numPr");
            var bullet = text.StartsWith('•') || text.StartsWith('·') ||
                (string?)numbering?.Element(W + "ilvl")?.Attribute(W + "val") == "1";
            if (sub.Success || (!bullet && (step is null || numbering is not null)))
            {
                step = new() { Instruction = sub.Success ? sub.Groups[1].Value : text, Details = [] };
                action.Steps!.Add(step);
            }
            else
            {
                var detail = bullet ? text.TrimStart('•', '·', ' ', '\t') : text;
                if (step is null) { step = new() { Instruction = detail, Details = [] }; action.Steps!.Add(step); }
                else step.Details!.Add(detail);
            }
        }
        if (actions.Count == 0) actions.Add(new() { Title = "", Steps = [new() { Instruction = "" }] });
        foreach (var item in actions.Where(item => item.Steps!.Count == 0)) item.Steps!.Add(new() { Instruction = "" });
        if (regrouped) warnings.Add("O procedimento não tinha ações no formato 1º, 2º… O texto foi agrupado em 'Procedimento importado'. Revise a divisão em ações e subetapas.");
        return actions;
    }

    private static void EnsureFits(KnowledgeArticle article)
    {
        void Limit(string value, int length) { if (value.Length > length) throw new DocumentImportException("Há texto maior que o limite de um campo. Reduza os textos no documento antes de importar; nenhum conteúdo foi cortado."); }
        Limit(article.Code, 40); Limit(article.Title, 160); Limit(article.Version, 11);
        Limit(article.Author, 120); Limit(article.Approver, 120); Limit(article.Executor, 120);
        Limit(article.Objective, 5000); Limit(article.Result, 5000); Limit(article.Requirements, 5000);
        var actions = article.Actions!;
        if (actions.Count > 20 || actions.Sum(action => action.Steps!.Count) > 100)
            throw new DocumentImportException("O POP deve ter no máximo 20 ações e 100 subetapas no total.");
        foreach (var action in actions)
        {
            Limit(action.Title, 200);
            foreach (var step in action.Steps!)
            {
                Limit(step.Instruction, 500);
                if (step.Details!.Count > 20) throw new DocumentImportException("Cada subetapa pode ter até 20 detalhes.");
                foreach (var detail in step.Details) Limit(detail, 2000);
            }
        }
    }

    private static XDocument Xml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw WrongTemplate();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes });
        return XDocument.Load(reader);
    }
    private static string ParagraphText(XElement paragraph) => string.Concat(paragraph.Descendants()
        .Where(node => node.Name == W + "t" || node.Name == W + "br" || node.Name == W + "cr" || node.Name == W + "tab")
        .Select(node => node.Name == W + "t" ? node.Value : node.Name == W + "tab" ? "\t" : "\n"));
    private static string CellText(XElement cell) => string.Join("\n", cell.Elements(W + "p").Select(ParagraphText)).Trim();
    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(character => character is not ('º' or 'ª') && CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)))
        .ToUpperInvariant();
    private static DocumentImportException WrongTemplate() => new("O arquivo não corresponde ao modelo CSTIC. Use um .docx com o cabeçalho de identificação e as cinco seções: Objetivo, Resultado, Requisitos, Executor e Procedimento.");
}
