using System.Security.Claims;
using System.Threading.RateLimiting;
using Cstic.KnowledgeBase.Data;
using Cstic.KnowledgeBase.Models;
using Cstic.KnowledgeBase.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DocumentImportService.MaxFileBytes + 65_536);
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = 200_000;
    options.MultipartBodyLengthLimit = DocumentImportService.MaxFileBytes;
    options.MemoryBufferThreshold = DocumentImportService.MaxFileBytes;
});

// Database and cookie keys live in the data folder, which must persist and be writable by the app.
var dataFolder = Path.GetFullPath(builder.Configuration["Dados:Pasta"] ?? "App_Data", builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataFolder);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(dataFolder, "cstic.db")}"));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataFolder, "chaves")))
    .SetApplicationName("Cstic.KnowledgeBase");

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
    options.Conventions.AllowAnonymousToPage("/Entrar");
    options.Conventions.AllowAnonymousToPage("/Cadastro");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AddPageRoute("/Pops/Editor", "/pops/{id:int}/editar");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/entrar";
    options.LogoutPath = "/sair";
    options.AccessDeniedPath = "/";
    options.Cookie.Name = "cstic.sessao";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        // Every request checks the account, so deactivation and password resets take effect immediately.
        var services = context.HttpContext.RequestServices;
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = int.TryParse(id, out var userId) ? await services.GetRequiredService<AppDbContext>().Users.FindAsync(userId) : null;
        if (user is null || user.Status != UserStatus.Ativo ||
            user.SecurityStamp != context.Principal?.FindFirstValue(SessionClaims.Stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        services.GetRequiredService<CurrentUser>().Account = user;
    };
    // Requests made by the form scripts receive a status code instead of the login page.
    options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole(nameof(UserRole.Administrador)))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<DocumentImportService>();
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<PopService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        !HttpMethods.IsPost(context.Request.Method)
        ? RateLimitPartition.GetNoLimiter("read")
        : RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Muitas solicitações. Aguarde um minuto e tente novamente." }, cancellationToken);
    };
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
    // Server-side recovery: dotnet Cstic.KnowledgeBase.dll redefinir-senha <login>
    if (args.Length == 2 && args[0] == "redefinir-senha")
    {
        try
        {
            var password = await accounts.EmergencyResetAsync(args[1]);
            Console.WriteLine($"Senha temporária de {AccountService.NormalizeLogin(args[1])}: {password}");
            Console.WriteLine("A troca será exigida no próximo acesso.");
            return 0;
        }
        catch (AccountException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
    var initial = app.Configuration.GetSection("AdministradorInicial");
    await accounts.EnsureInitialAdminAsync(initial["Login"] ?? "adm.audi", initial["NomeCompleto"] ?? "Administrador CSTIC",
        initial["Funcao"] ?? "Qualidade", initial["Senha"] ?? throw new InvalidOperationException("Defina AdministradorInicial:Senha."));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    if (HttpMethods.IsPost(context.Request.Method))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    // A temporary password (first access or reset by the administrator) must be replaced before anything else.
    var user = context.RequestServices.GetRequiredService<CurrentUser>().Account;
    if (user?.MustChangePassword == true &&
        !context.Request.Path.StartsWithSegments("/conta/senha") && !context.Request.Path.StartsWithSegments("/sair"))
    {
        if (context.Request.Headers.ContainsKey(SessionClaims.FetchHeader))
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else context.Response.Redirect("/conta/senha");
        return;
    }
    await next();
});
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();
return 0;

static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
{
    if (context.Request.Headers.ContainsKey(SessionClaims.FetchHeader)) context.Response.StatusCode = status;
    else context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program { }
