using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Cstic.KnowledgeBase.Models;

namespace Cstic.KnowledgeBase.Services;

public sealed class DocumentService
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private readonly byte[] _template;
    private readonly TimeProvider _clock;

    public DocumentService(TimeProvider clock)
    {
        _clock = clock;
        using var stream = typeof(DocumentService).Assembly.GetManifestResourceStream(
            "Cstic.KnowledgeBase.Templates.POP_BasedeConhecimento_CSTIC.docx")
            ?? throw new InvalidOperationException("O modelo Word não foi incorporado à aplicação.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        _template = memory.ToArray();
    }

    public string CreationDate => _clock.GetUtcNow().ToOffset(TimeSpan.FromHours(-3))
        .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <param name="date">Date of the header; defaults to now (validated versions keep their validation date).</param>
    public byte[] CreateWord(KnowledgeArticle article, DateTimeOffset? date = null)
    {
        if (ArticleValidator.Validate(article).Count != 0)
            throw new ArgumentException("O documento contém campos inválidos.", nameof(article));
        var instant = date ?? _clock.GetUtcNow();
        var headerDate = instant.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        using var input = new MemoryStream(_template, false);
        using var original = new ZipArchive(input, ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var generated = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in original.Entries)
            {
                var target = generated.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var source = entry.Open();
                using var destination = target.Open();
                var editable = entry.FullName == "word/document.xml" ||
                    entry.FullName.StartsWith("word/header", StringComparison.Ordinal) ||
                    entry.FullName.StartsWith("word/footer", StringComparison.Ordinal) ||
                    entry.FullName is "docProps/core.xml" or "word/settings.xml";
                if (!editable) { source.CopyTo(destination); continue; }
                using var reader = XmlReader.Create(source, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                var xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
                if (entry.FullName == "word/document.xml") FillBody(xml, article);
                else if (entry.FullName.StartsWith("word/header", StringComparison.Ordinal)) FillHeader(xml, article, headerDate);
                else if (entry.FullName.StartsWith("word/footer", StringComparison.Ordinal)) FillFooter(xml, article);
                else if (entry.FullName == "docProps/core.xml") FillProperties(xml, article, instant);
                else if (entry.FullName == "word/settings.xml")
                {
                    xml.Root!.Elements(W + "updateFields").Remove();
                    xml.Root.Add(new XElement(W + "updateFields", new XAttribute(W + "val", "true")));
                }
                xml.Save(destination, SaveOptions.DisableFormatting);
            }
        }
        return output.ToArray();
    }

    private static void FillHeader(XDocument xml, KnowledgeArticle article, string date)
    {
        var metadata = xml.Root!.Elements(W + "tbl").Skip(1).FirstOrDefault();
        if (metadata is null) return; // The supplied template has an unused blank first-page header.
        var rows = metadata.Elements(W + "tr").ToArray();
        if (rows.Length != 6) throw new InvalidOperationException("O cabeçalho do modelo deve ter seis linhas.");
        string[] values = [article.Code.Trim(), article.Title.Trim(), article.Version.Trim(), date,
            article.Author.Trim(), article.Approver.Trim()];
        for (var i = 0; i < rows.Length; i++)
        {
            var cell = rows[i].Elements(W + "tc").Last();
            var paragraphProperties = cell.Element(W + "p")?.Element(W + "pPr");
            var paragraph = Paragraph(values[i]);
            if (paragraphProperties is not null)
                paragraph.Element(W + "pPr")!.ReplaceWith(new XElement(paragraphProperties));
            ReplaceCell(cell, [paragraph]);
        }
    }

    private static void FillBody(XDocument xml, KnowledgeArticle article)
    {
        var body = xml.Root!.Element(W + "body")!;
        var tables = body.Elements(W + "tbl").ToArray();
        if (tables.Length != 5) throw new InvalidOperationException("O corpo do modelo deve ter cinco tabelas.");
        string[] text = [article.Objective, article.Result, article.Requirements, article.Executor];
        for (var i = 0; i < 4; i++)
        {
            var row = tables[i].Elements(W + "tr").Last();
            SetMinimumHeight(row, 700);
            ReplaceCell(row.Elements(W + "tc").Single(), [Paragraph(text[i].Trim())]);
        }
        var procedureRow = tables[4].Elements(W + "tr").Last();
        SetMinimumHeight(procedureRow, 6100);
        var paragraphs = new List<XElement>();
        for (var i = 0; i < article.Actions!.Count; i++)
        {
            var action = article.Actions[i];
            paragraphs.Add(Paragraph($"{i + 1}º  {action.Title.Trim()}", bold: true,
                keepNext: true, spaceBefore: i == 0 ? 160 : 200));
            for (var j = 0; j < action.Steps!.Count; j++)
            {
                var step = action.Steps[j];
                var details = step.Details!.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
                // As requested, every action starts again at 1.1, including the second/third actions.
                paragraphs.Add(Paragraph($"1.{j + 1}  {step.Instruction.Trim()}",
                    keepNext: details.Length > 0, indent: 140, spaceBefore: 80));
                foreach (var detail in details)
                    paragraphs.Add(Paragraph("•  " + detail.Trim(), indent: 420));
            }
        }
        ReplaceCell(procedureRow.Elements(W + "tc").Single(), paragraphs);
        // Allow long procedures to flow across pages, repeating the grey section heading.
        var heading = tables[4].Elements(W + "tr").First();
        var properties = heading.Element(W + "trPr");
        if (properties is null) { properties = new XElement(W + "trPr"); heading.AddFirst(properties); }
        properties.Add(new XElement(W + "tblHeader"));
        procedureRow.Element(W + "trPr")?.Elements(W + "cantSplit").Remove();
        var trailing = tables[4].ElementsAfterSelf(W + "p").ToArray();
        foreach (var paragraph in trailing.Skip(1)) paragraph.Remove();
    }

    private static void SetMinimumHeight(XElement row, int height)
    {
        var properties = row.Element(W + "trPr");
        if (properties is null) { properties = new XElement(W + "trPr"); row.AddFirst(properties); }
        properties.Elements(W + "trHeight").Remove();
        properties.Add(new XElement(W + "trHeight", new XAttribute(W + "val", height),
            new XAttribute(W + "hRule", "atLeast")));
    }

    private static void ReplaceCell(XElement cell, IEnumerable<XElement> paragraphs)
    {
        cell.Elements().Where(element => element.Name != W + "tcPr").Remove();
        cell.Add(paragraphs);
    }

    private static XElement Paragraph(string text, bool bold = false, bool keepNext = false,
        int indent = 0, int spaceBefore = 0)
    {
        var properties = new XElement(W + "pPr",
            new XElement(W + "pStyle", new XAttribute(W + "val", "Normal")),
            keepNext ? new XElement(W + "keepNext") : null,
            new XElement(W + "spacing", new XAttribute(W + "before", spaceBefore),
                new XAttribute(W + "after", 40), new XAttribute(W + "line", 240),
                new XAttribute(W + "lineRule", "auto")));
        if (indent > 0) properties.Add(new XElement(W + "ind", new XAttribute(W + "left", indent)));
        var runProperties = new XElement(W + "rPr",
            new XElement(W + "rFonts", new XAttribute(W + "ascii", "Arial"), new XAttribute(W + "hAnsi", "Arial")),
            bold ? new XElement(W + "b") : null,
            new XElement(W + "color", new XAttribute(W + "val", "000000")),
            new XElement(W + "sz", new XAttribute(W + "val", 20)),
            new XElement(W + "szCs", new XAttribute(W + "val", 20)),
            new XElement(W + "lang", new XAttribute(W + "val", "pt-BR")));
        var run = new XElement(W + "r", runProperties);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i != 0) run.Add(new XElement(W + "br"));
            run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[i]));
        }
        return new XElement(W + "p", properties, run);
    }

    private static void FillFooter(XDocument xml, KnowledgeArticle article)
    {
        foreach (var text in xml.Descendants(W + "t"))
            if (text.Value.Contains("Nome da Base de Conhecimento", StringComparison.Ordinal))
                text.Value = text.Value.Replace("Nome da Base de Conhecimento", article.Title.Trim(), StringComparison.Ordinal);
        // PAGE fields, tabs and dot leaders from the original document remain intact.
    }

    private static void FillProperties(XDocument xml, KnowledgeArticle article, DateTimeOffset instant)
    {
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
        XNamespace dates = "http://purl.org/dc/terms/";
        void Set(XName name, string value)
        {
            var element = xml.Root!.Element(name);
            if (element is null) xml.Root.Add(new XElement(name, value));
            else element.Value = value;
        }
        Set(dc + "title", article.Title.Trim());
        Set(dc + "creator", article.Author.Trim());
        Set(cp + "lastModifiedBy", article.Author.Trim());
        Set(cp + "revision", "1");
        var timestamp = instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        Set(dates + "created", timestamp);
        Set(dates + "modified", timestamp);
    }

    public static string FileName(KnowledgeArticle article)
    {
        var original = $"POP_{article.Code}_{article.Title}".Normalize(NormalizationForm.FormD);
        var safe = new StringBuilder();
        foreach (var character in original)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_') safe.Append(character);
            else if (safe.Length > 0 && safe[^1] != '_') safe.Append('_');
            if (safe.Length == 110) break;
        }
        return safe.ToString().TrimEnd('_') + ".docx";
    }
}
