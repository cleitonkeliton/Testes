using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages.Conta;

public sealed class SenhaModel(AccountService accounts, CurrentUser currentUser) : PageModel
{
    public string? Error { get; private set; }
    public bool Required => currentUser.Required.MustChangePassword;
    public string Login => currentUser.Required.Login;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? current, string? password, string? confirmation)
    {
        var user = currentUser.Required;
        try
        {
            await accounts.ChangePasswordAsync(user, current, password, confirmation);
        }
        catch (AccountException exception)
        {
            Error = exception.Message;
            return Page();
        }
        // The password change renews the security stamp, which ends other sessions; renew this one.
        await SessionClaims.SignInAsync(HttpContext, user);
        TempData["Notice"] = "Senha alterada.";
        return Redirect("/");
    }
}
