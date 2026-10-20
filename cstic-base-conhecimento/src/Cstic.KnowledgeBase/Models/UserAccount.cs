namespace Cstic.KnowledgeBase.Models;

public enum UserRole { Tecnico, Administrador }

public enum UserStatus { Pendente, Ativo, Inativo }

public sealed class UserAccount
{
    public static readonly string[] Functions = ["Técnico N1", "Técnico N2", "Supervisor", "Qualidade", "Outro"];

    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Function { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Tecnico;
    public UserStatus Status { get; set; } = UserStatus.Pendente;
    public string PasswordHash { get; set; } = "";
    public bool MustChangePassword { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    /// <summary>Changes whenever access must be revoked, signing out existing sessions.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public int? ApprovedById { get; set; }

    public bool IsAdmin => Role == UserRole.Administrador;
    public string RoleLabel => IsAdmin ? "Administrador" : "Técnico";
    public string StatusLabel => Status switch
    {
        UserStatus.Pendente => "Aguardando aprovação",
        UserStatus.Ativo => "Ativo",
        _ => "Desativado"
    };
}
