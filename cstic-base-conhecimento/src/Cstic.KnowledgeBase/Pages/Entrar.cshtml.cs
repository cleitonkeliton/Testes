using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cstic.KnowledgeBase.Pages;

public sealed class EntrarModel(AccountService accounts) : PageModel
{
    public string? Error { get; private set; }
    public string? Login { get; private set; }
    public string? ReturnUrl { get; private set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? login, string? password, string? returnUrl)
    {
        var result = await accounts.SignInAsync(login, password);
        if (result.User is null)
        {
            Error = result.Error;
            Login = AccountService.NormalizeLogin(login);
            ReturnUrl = returnUrl;
            return Page();
        }
        await SessionClaims.SignInAsync(HttpContext, result.User);
        if (result.User.MustChangePassword) return Redirect("/conta/senha");
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
