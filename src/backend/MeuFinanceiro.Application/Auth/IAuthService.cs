namespace MeuFinanceiro.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Sempre retorna sem erro, exista ou não o e-mail — evita enumeração de contas.</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Revoga todos os refresh tokens ativos do usuário ao concluir com sucesso.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
