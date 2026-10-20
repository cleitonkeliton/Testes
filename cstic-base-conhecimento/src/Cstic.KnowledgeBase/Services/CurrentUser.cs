using System.Security.Claims;
using Cstic.KnowledgeBase.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Cstic.KnowledgeBase.Services;

/// <summary>The signed-in account, loaded once per request when the session cookie is validated.</summary>
public sealed class CurrentUser
{
    public UserAccount? Account { get; set; }
    public UserAccount Required => Account ?? throw new InvalidOperationException("Nenhum usuário autenticado.");
}

public static class SessionClaims
{
    public const string Stamp = "cstic:stamp";
    /// <summary>Header sent by the page scripts, so expired sessions answer 401 instead of redirecting.</summary>
    public const string FetchHeader = "X-Cstic-Fetch";

    public static Task SignInAsync(HttpContext context, UserAccount user)
    {
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(Stamp, user.SecurityStamp)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
