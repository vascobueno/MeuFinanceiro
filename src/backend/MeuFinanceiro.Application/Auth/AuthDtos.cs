using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Auth;

// CaptchaToken tem default vazio pra não quebrar todo teste/chamador que ainda não manda um
// (a verificação em si só bloqueia de verdade se Turnstile:SecretKey estiver configurado —
// TurnstileCaptchaVerifier.VerifyAsync).
public record RegisterRequest(string Name, string Username, string Email, string Password, string CaptchaToken = "");
public record LoginRequest(string Username, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email, string CaptchaToken = "");
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ConfirmEmailRequest(string Email, string Token);
/// <summary>Por Username, não Email — é o que a tela de login já tem em mãos quando o login falha por e-mail não confirmado.</summary>
public record ResendConfirmationRequest(string Username);

/// <summary>Registro não loga mais automaticamente — precisa confirmar o e-mail primeiro.</summary>
public record RegisterResponse(string Message);

/// <summary>
/// Access token de vida curta (1h) + refresh token (7 dias) — decisão de
/// autenticação em docs/12-seguranca.md.
/// </summary>
public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserResponse User);

public record UserResponse(Guid Id, string Name, string Email, Guid GrupoId, GrupoRole GrupoRole, GrupoAccessProfile AccessProfile, bool IsAdministrator, bool MustChangePassword);
