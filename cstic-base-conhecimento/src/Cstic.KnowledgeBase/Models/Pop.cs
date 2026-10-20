namespace Cstic.KnowledgeBase.Models;

public enum PopStatus { Rascunho, Enviado, Devolvido, Validado }

public enum PopEventType { Criado, Importado, Enviado, Devolvido, Validado, RevisaoIniciada }

/// <summary>A POP and its current working content, owned by the technician who created or imported it.</summary>
public sealed class Pop
{
    public int Id { get; set; }
    /// <summary>Number shown in the document header. Assigned automatically on the first validation.</summary>
    public string? Code { get; set; }
    /// <summary>Last validated version; null until the POP is validated for the first time.</summary>
    public string? Version { get; set; }
    public string Title { get; set; } = "";
    public int AuthorId { get; set; }
    public UserAccount Author { get; set; } = null!;
    public PopStatus Status { get; set; } = PopStatus.Rascunho;
    /// <summary>JSON of <see cref="KnowledgeArticle"/> with the editable content.</summary>
    public string ContentJson { get; set; } = "{}";
    public string? ReturnComment { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public List<PopEvent> Events { get; set; } = [];
    public List<PopVersion> Versions { get; set; } = [];

    public string StatusLabel => Status switch
    {
        PopStatus.Rascunho => Version is null ? "Rascunho" : "Revisão em rascunho",
        PopStatus.Enviado => "Aguardando validação",
        PopStatus.Devolvido => "Devolvido para ajuste",
        _ => "Validado"
    };
    public bool IsEditable => Status is PopStatus.Rascunho or PopStatus.Devolvido or PopStatus.Validado;
}

public sealed class PopEvent
{
    public int Id { get; set; }
    public int PopId { get; set; }
    public int UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    public PopEventType Type { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedUtc { get; set; }

    public string TypeLabel => Type switch
    {
        PopEventType.Criado => "Criado",
        PopEventType.Importado => "Importado de arquivo Word",
        PopEventType.Enviado => "Enviado para validação",
        PopEventType.Devolvido => "Devolvido ao técnico",
        PopEventType.Validado => "Validado",
        _ => "Revisão iniciada"
    };
}

/// <summary>Immutable copy of the document exactly as validated.</summary>
public sealed class PopVersion
{
    public int Id { get; set; }
    public int PopId { get; set; }
    public string Code { get; set; } = "";
    public string Version { get; set; } = "";
    /// <summary>JSON of the complete <see cref="KnowledgeArticle"/>, header fields included.</summary>
    public string ContentJson { get; set; } = "{}";
    public int ValidatedById { get; set; }
    public UserAccount ValidatedBy { get; set; } = null!;
    public DateTime ValidatedUtc { get; set; }
}
