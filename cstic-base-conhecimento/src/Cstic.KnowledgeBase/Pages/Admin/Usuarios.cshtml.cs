using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Admin;

public sealed class UsuariosModel(AccountService accounts, CurrentUser current) : PageModel
{
    public List<UserAccount> Users { get; private set; } = [];
    public UserAccount Me => current.Required;
    public string? NewFullName { get; private set; }
    public string? NewFunction { get; private set; }
    public string? NewLogin { get; private set; }
    public UserRole NewRole { get; private set; }

    public async Task OnGetAsync() => Users = await accounts.ListAsync();

    public Task<IActionResult> OnPostApproveAsync(int id, UserRole role) =>
        Run(() => accounts.ApproveAsync(Me, id, role), "Cadastro aprovado. A pessoa já pode entrar.");

    public Task<IActionResult> OnPostRejectAsync(int id) =>
        Run(() => accounts.RejectAsync(id), "Cadastro recusado e removido.");

    public Task<IActionResult> OnPostUpdateAsync(int id, string? fullName, string? function, UserRole role) =>
        Run(() => accounts.UpdateAsync(Me, id, fullName, function, role), "Dados atualizados.");

    public Task<IActionResult> OnPostResetAsync(int id, string? password, string? confirmation) =>
        Run(() => accounts.ResetPasswordAsync(id, password, confirmation), "Senha redefinida. A troca será exigida no próximo acesso.");

    public Task<IActionResult> OnPostActiveAsync(int id, bool active) =>
        Run(() => accounts.SetActiveAsync(Me, id, active), active ? "Acesso reativado." : "Acesso desativado.");

    public async Task<IActionResult> OnPostCreateAsync(string? fullName, string? function, string? login, UserRole role,
        string? password, string? confirmation)
    {
        try
        {
            var user = await accounts.CreateByAdminAsync(Me, fullName, function, login, role, password, confirmation);
            TempData["Notice"] = $"Cadastro criado com o login {user.Login}. Informe a senha temporária à pessoa.";
            return Redirect("/admin/usuarios");
        }
        catch (AccountException exception)
        {
            TempData["Error"] = exception.Message;
            (NewFullName, NewFunction, NewLogin, NewRole) = (fullName, function, login, role);
            Users = await accounts.ListAsync();
            return Page();
        }
    }

    private async Task<IActionResult> Run(Func<Task> action, string success)
    {
        try
        {
            await action();
            TempData["Notice"] = success;
        }
        catch (AccountException exception) { TempData["Error"] = exception.Message; }
        return Redirect("/admin/usuarios");
    }
}
