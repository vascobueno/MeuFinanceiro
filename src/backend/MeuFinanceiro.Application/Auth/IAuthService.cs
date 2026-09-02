namespace MeuFinanceiro.Application.Auth;

public interface IAuthService
{
    /// <summary>Não loga automaticamente — manda um e-mail de confirmação e exige clicar antes do primeiro login.</summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Sempre retorna sem erro, exista ou não o e-mail — evita enumeração de contas.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Revoga todos os refresh tokens ativos do usuário ao concluir com sucesso.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Usada tanto pela troca de senha voluntária quanto pela troca obrigatória de senha
    /// temporária (MustChangePassword) — limpa o flag e confirma o e-mail ao concluir.
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Mesma mensagem genérica pra "e-mail não existe" e "token inválido/expirado" (evita enumeração).</summary>
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);

    /// <summary>Sempre retorna sem erro, exista ou não o e-mail, ou já esteja confirmado.</summary>
    Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken ct = default);

    /// <summary>
    /// "Quem sou eu" — usado pelo frontend pra rehidratar a sessão a partir do cookie
    /// httpOnly após um F5 (não dá pra ler o cookie via JavaScript pra saber se está logado).
    /// </summary>
    Task<UserResponse> GetCurrentUserAsync(CancellationToken ct = default);
}
