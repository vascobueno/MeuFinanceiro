using Microsoft.AspNetCore.Identity;

namespace MeuFinanceiro.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity para gestão de usuário/senha (hash+salt resolvidos pelo
/// framework) — decisão registrada em docs/12-seguranca.md.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
