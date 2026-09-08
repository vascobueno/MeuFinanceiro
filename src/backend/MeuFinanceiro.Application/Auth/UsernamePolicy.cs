using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Auth;

/// <summary>
/// Regra de formato de nome de usuário, compartilhada entre a validação de cadastro
/// (AuthService) e a correção de contas antigas (LegacyUsernameBackfiller) — evita duas
/// cópias divergentes da mesma regra dentro do backend.
/// </summary>
public static class UsernamePolicy
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    public static readonly Regex Pattern = new($"^[a-zA-Z0-9_.-]{{{MinLength},{MaxLength}}}$");

    /// <summary>Caracteres fora deste conjunto são removidos ao derivar um username a partir de outro texto.</summary>
    public static readonly Regex DisallowedChars = new("[^a-zA-Z0-9_.-]");
}
