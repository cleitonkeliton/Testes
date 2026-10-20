using System.ComponentModel.DataAnnotations;

namespace Cstic.KnowledgeBase.Models;

public sealed class KnowledgeArticle
{
    [Required(ErrorMessage = "Informe o número do POP.")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._/-]{0,39}$", ErrorMessage = "Use até 40 letras, números, pontos, traços, barras ou sublinhados.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome da base de conhecimento.")]
    [StringLength(160, ErrorMessage = "O nome deve ter até 160 caracteres.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Informe a versão.")]
    [RegularExpression(@"^\d{1,3}(\.\d{1,3}){0,2}$", ErrorMessage = "Use uma versão como 1, 1.0 ou 1.0.1.")]
    public string Version { get; set; } = "1.0";

    [Required(ErrorMessage = "Informe o nome do técnico autor.")]
    [StringLength(120, ErrorMessage = "O nome do autor deve ter até 120 caracteres.")]
    public string Author { get; set; } = "";

    [Required(ErrorMessage = "Informe o nome do aprovador.")]
    [StringLength(120, ErrorMessage = "O nome do aprovador deve ter até 120 caracteres.")]
    public string Approver { get; set; } = "";

    [Required(ErrorMessage = "Descreva o objetivo do procedimento.")]
    [StringLength(5000, ErrorMessage = "O objetivo deve ter até 5.000 caracteres.")]
    public string Objective { get; set; } = "";

    [Required(ErrorMessage = "Descreva o resultado esperado.")]
    [StringLength(5000, ErrorMessage = "O resultado deve ter até 5.000 caracteres.")]
    public string Result { get; set; } = "";

    [Required(ErrorMessage = "Informe os requisitos ou escreva Não se aplica.")]
    [StringLength(5000, ErrorMessage = "Os requisitos devem ter até 5.000 caracteres.")]
    public string Requirements { get; set; } = "";

    [Required(ErrorMessage = "Selecione o executor.")]
    [RegularExpression("^(N1|N2|N2 / N1)$", ErrorMessage = "Selecione N1, N2 ou N2 / N1.")]
    public string Executor { get; set; } = "N2 / N1";

    public List<ProcedureAction>? Actions { get; set; } = [];
}

public sealed class ProcedureAction
{
    [Required(ErrorMessage = "Informe o nome da ação principal.")]
    [StringLength(200, ErrorMessage = "O nome da ação deve ter até 200 caracteres.")]
    public string Title { get; set; } = "";

    public List<ProcedureStep>? Steps { get; set; } = [];
}

public sealed class ProcedureStep
{
    [Required(ErrorMessage = "Descreva a ação desta etapa.")]
    [StringLength(500, ErrorMessage = "A ação deve ter até 500 caracteres.")]
    public string Instruction { get; set; } = "";

    public List<string>? Details { get; set; } = [];
}
