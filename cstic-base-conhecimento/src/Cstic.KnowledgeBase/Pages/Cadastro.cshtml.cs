using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages;

public sealed class CadastroModel(AccountService accounts) : PageModel
{
    public string? Error { get; private set; }
    public string? FullName { get; private set; }
    public string? Function { get; private set; }
    public string? Login { get; private set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? Redirect("/") : Page();

    public async Task<IActionResult> OnPostAsync(string? fullName, string? function, string? login,
        string? password, string? confirmation)
    {
        try
        {
            var user = await accounts.RegisterAsync(fullName, function, login, password, confirmation);
            TempData["Notice"] = $"Cadastro enviado. Seu login é {user.Login}. Você poderá entrar após a aprovação do administrador.";
            return Redirect("/entrar");
        }
        catch (AccountException exception)
        {
            (Error, FullName, Function, Login) = (exception.Message, fullName, function, login);
            return Page();
        }
    }
}
