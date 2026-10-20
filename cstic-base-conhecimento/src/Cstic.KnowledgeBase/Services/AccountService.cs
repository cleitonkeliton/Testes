using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Cstic.KnowledgeBase.Data;
using Cstic.KnowledgeBase.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cstic.KnowledgeBase.Services;

public sealed class AccountException(string message) : Exception(message);

public sealed record LoginResult(UserAccount? User, string? Error);

/// <summary>Registration, sign-in rules and user administration.</summary>
public sealed class AccountService(AppDbContext db, TimeProvider clock, IPasswordHasher<UserAccount> hasher)
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly Regex LoginFormat = new(@"^[a-z]+\.[a-z]+[0-9]{0,2}$", RegexOptions.CultureInvariant);
    // Hash of a random password, verified for unknown logins so the response time does not reveal which logins exist.
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<UserAccount>().HashPassword(new UserAccount(), Convert.ToHexString(RandomNumberGenerator.GetBytes(16))));

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Lowercase, without accents or spaces: "José.Silva " becomes "jose.silva".</summary>
    public static string NormalizeLogin(string? login) => string.Concat((login ?? "").Trim().ToLowerInvariant()
        .Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));

    public async Task<UserAccount> RegisterAsync(string? fullName, string? function, string? login,
        string? password, string? confirmation)
    {
        var user = await NewUserAsync(fullName, function, login, password, confirmation);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<UserAccount> CreateByAdminAsync(UserAccount admin, string? fullName, string? function,
        string? login, UserRole role, string? password, string? confirmation)
    {
        var user = await NewUserAsync(fullName, function, login, password, confirmation);
        user.Role = role;
        user.Status = UserStatus.Ativo;
        user.MustChangePassword = true;
        user.ApprovedUtc = Now;
        user.ApprovedById = admin.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<UserAccount> NewUserAsync(string? fullName, string? function, string? login,
        string? password, string? confirmation)
    {
        var name = CleanName(fullName);
        var normalized = NormalizeLogin(login);
        CheckFunction(function);
        if (!LoginFormat.IsMatch(normalized) || normalized.Length > 60)
            throw new AccountException("Use o login no formato primeironome.ultimonome, só com letras (ex.: maria.silva). Para nomes repetidos, acrescente um número: maria.silva2.");
        if (await db.Users.AnyAsync(user => user.Login == normalized))
            throw new AccountException($"O login {normalized} já está em uso. Acrescente um número, por exemplo {normalized}2.");
        CheckPassword(password, confirmation, normalized);
        var user = new UserAccount { FullName = name, Function = function!, Login = normalized, CreatedUtc = Now };
        user.PasswordHash = hasher.HashPassword(user, password!);
        return user;
    }

    public async Task<LoginResult> SignInAsync(string? login, string? password)
    {
        const string invalid = "Login ou senha incorretos.";
        var normalized = NormalizeLogin(login);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Login == normalized);
        if (user is null || string.IsNullOrEmpty(password))
        {
            hasher.VerifyHashedPassword(new UserAccount(), DummyHash.Value, password ?? "");
            return new(null, invalid);
        }
        if (user.LockoutEndUtc > Now)
            return new(null, $"Muitas tentativas incorretas. Tente novamente em {Math.Ceiling((user.LockoutEndUtc.Value - Now).TotalMinutes)} minuto(s).");
        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLogins++;
            if (user.FailedLogins >= MaxFailedLogins)
            {
                user.FailedLogins = 0;
                user.LockoutEndUtc = Now + LockoutDuration;
            }
            await db.SaveChangesAsync();
            return new(null, invalid);
        }
        // Status is only revealed to whoever knows the password.
        if (user.Status == UserStatus.Pendente)
            return new(null, "Seu cadastro ainda aguarda a aprovação do administrador.");
        if (user.Status == UserStatus.Inativo)
            return new(null, "Seu acesso está desativado. Procure o administrador.");
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, password);
        user.FailedLogins = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync();
        return new(user, null);
    }

    public async Task ChangePasswordAsync(UserAccount user, string? current, string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(current) ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
            throw new AccountException("A senha atual está incorreta.");
        CheckPassword(password, confirmation, user.Login);
        if (password == current) throw new AccountException("A nova senha deve ser diferente da atual.");
        user.PasswordHash = hasher.HashPassword(user, password!);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    public Task<List<UserAccount>> ListAsync() =>
        db.Users.OrderBy(user => user.Status).ThenBy(user => user.FullName).ToListAsync();

    public Task<int> PendingCountAsync() => db.Users.CountAsync(user => user.Status == UserStatus.Pendente);

    public async Task ApproveAsync(UserAccount admin, int userId, UserRole role)
    {
        var user = await FindAsync(userId);
        if (user.Status != UserStatus.Pendente) throw new AccountException("Este cadastro já foi analisado.");
        user.Status = UserStatus.Ativo;
        user.Role = role;
        user.ApprovedUtc = Now;
        user.ApprovedById = admin.Id;
        await db.SaveChangesAsync();
    }

    /// <summary>Discards a pending registration, so the login can be registered again.</summary>
    public async Task RejectAsync(int userId)
    {
        var user = await FindAsync(userId);
        if (user.Status != UserStatus.Pendente) throw new AccountException("Apenas cadastros pendentes podem ser recusados.");
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserAccount admin, int userId, string? fullName, string? function, UserRole role)
    {
        var user = await FindAsync(userId);
        var name = CleanName(fullName);
        CheckFunction(function);
        if (user.IsAdmin && role != UserRole.Administrador) await EnsureAnotherAdminAsync(admin, user);
        if (user.Role != role) user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.FullName = name;
        user.Function = function!;
        user.Role = role;
        await db.SaveChangesAsync();
    }

    /// <summary>Sets a temporary password; the current one is never shown and the user must replace it.</summary>
    public async Task ResetPasswordAsync(int userId, string? password, string? confirmation)
    {
        var user = await FindAsync(userId);
        CheckPassword(password, confirmation, user.Login);
        user.PasswordHash = hasher.HashPassword(user, password!);
        user.MustChangePassword = true;
        user.FailedLogins = 0;
        user.LockoutEndUtc = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(UserAccount admin, int userId, bool active)
    {
        var user = await FindAsync(userId);
        if (user.Status == UserStatus.Pendente) throw new AccountException("Aprove ou recuse o cadastro pendente.");
        if (!active && user.IsAdmin) await EnsureAnotherAdminAsync(admin, user);
        user.Status = active ? UserStatus.Ativo : UserStatus.Inativo;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
    }

    /// <summary>Creates the first administrator when the database has none.</summary>
    public async Task EnsureInitialAdminAsync(string login, string fullName, string function, string password)
    {
        if (await db.Users.AnyAsync(user => user.Role == UserRole.Administrador)) return;
        var user = new UserAccount
        {
            Login = NormalizeLogin(login), FullName = fullName, Function = function, Role = UserRole.Administrador,
            Status = UserStatus.Ativo, MustChangePassword = true, CreatedUtc = Now, ApprovedUtc = Now
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    /// <summary>Server-side recovery: reactivates the account and returns a random temporary password.</summary>
    public async Task<string> EmergencyResetAsync(string login)
    {
        var normalized = NormalizeLogin(login);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Login == normalized)
            ?? throw new AccountException($"Login {normalized} não encontrado.");
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var password = RandomNumberGenerator.GetString(alphabet, 14) + "7a";
        user.PasswordHash = hasher.HashPassword(user, password);
        user.Status = UserStatus.Ativo;
        user.MustChangePassword = true;
        user.FailedLogins = 0;
        user.LockoutEndUtc = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        return password;
    }

    private async Task EnsureAnotherAdminAsync(UserAccount admin, UserAccount target)
    {
        if (target.Id == admin.Id)
            throw new AccountException("Você não pode remover o seu próprio acesso de administrador.");
        if (!await db.Users.AnyAsync(user => user.Id != target.Id && user.Role == UserRole.Administrador && user.Status == UserStatus.Ativo))
            throw new AccountException("É necessário manter pelo menos um administrador ativo.");
    }

    private async Task<UserAccount> FindAsync(int userId) =>
        await db.Users.FindAsync(userId) ?? throw new AccountException("Usuário não encontrado.");

    private static string CleanName(string? fullName)
    {
        var name = string.Join(' ', (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (name.Split(' ').Length < 2 || name.Length > 120 || name.Any(char.IsControl))
            throw new AccountException("Informe o nome completo, com nome e sobrenome (até 120 caracteres).");
        return name;
    }

    private static void CheckFunction(string? function)
    {
        if (!UserAccount.Functions.Contains(function))
            throw new AccountException("Selecione uma função da lista.");
    }

    public static void CheckPassword(string? password, string? confirmation, string login)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 10 || password.Length > 128 ||
            !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new AccountException("A senha deve ter de 10 a 128 caracteres, com pelo menos uma letra e um número.");
        if (password.Contains(login, StringComparison.OrdinalIgnoreCase))
            throw new AccountException("A senha não pode conter o login.");
        if (password != confirmation) throw new AccountException("A confirmação não corresponde à senha.");
    }
}
