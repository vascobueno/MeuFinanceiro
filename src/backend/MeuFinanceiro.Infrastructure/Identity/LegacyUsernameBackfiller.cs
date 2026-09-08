using System.Globalization;
using System.Text;
using MeuFinanceiro.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MeuFinanceiro.Infrastructure.Identity;

/// <summary>
/// Comando manual único (não roda no startup normal) — corrige contas cujo UserName ainda é
/// igual ao Email (marca de conta criada antes do login por nome de usuário), definindo um
/// UserName novo derivado do Name (sem espaços/acentos). Invocar via
/// `dotnet run --project src/backend/MeuFinanceiro.Api -- --corrigir-usernames-antigos`.
/// Idempotente: uma conta já corrigida não bate mais em UserName == Email, então rodar de
/// novo não faz nada com ela.
/// </summary>
public static class LegacyUsernameBackfiller
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("LegacyUsernameBackfiller");

        var legacyUsers = await userManager.Users
            .Where(u => u.NormalizedUserName == u.NormalizedEmail)
            .ToListAsync();

        if (legacyUsers.Count == 0)
        {
            logger.LogInformation("Nenhuma conta antiga encontrada (UserName == Email). Nada a corrigir.");
            return;
        }

        foreach (var user in legacyUsers)
        {
            var baseUsername = NormalizeToUsername(user.Name);
            var candidate = await FindAvailableUsernameAsync(userManager, user.Id, baseUsername);

            var oldUsername = user.UserName;
            var result = await userManager.SetUserNameAsync(user, candidate);

            if (result.Succeeded)
                logger.LogInformation("Conta corrigida: {OldUsername} -> {NewUsername} (Name: {Name})", oldUsername, candidate, user.Name);
            else
                logger.LogWarning("Falha ao corrigir {OldUsername}: {Errors}", oldUsername, string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("Correção concluída. {Count} conta(s) atualizada(s).", legacyUsers.Count);
    }

    private static string NormalizeToUsername(string name)
    {
        var withoutDiacritics = RemoveDiacritics(name);
        var stripped = UsernamePolicy.DisallowedChars.Replace(withoutDiacritics, "");

        if (stripped.Length < UsernamePolicy.MinLength)
            stripped = stripped.PadRight(UsernamePolicy.MinLength, '0');
        if (stripped.Length > UsernamePolicy.MaxLength)
            stripped = stripped[..UsernamePolicy.MaxLength];

        return stripped;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static async Task<string> FindAvailableUsernameAsync(UserManager<ApplicationUser> userManager, Guid currentUserId, string baseUsername)
    {
        var candidate = baseUsername;
        var suffix = 1;

        while (true)
        {
            var existing = await userManager.FindByNameAsync(candidate);
            if (existing is null || existing.Id == currentUserId)
                return candidate;

            suffix++;
            var suffixText = suffix.ToString();
            candidate = baseUsername.Length + suffixText.Length > UsernamePolicy.MaxLength
                ? baseUsername[..(UsernamePolicy.MaxLength - suffixText.Length)] + suffixText
                : baseUsername + suffixText;
        }
    }
}
