using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>
/// Access token de vida curta (15 min) + refresh token (7 dias) — decisão de
/// autenticação em docs/12-seguranca.md.
/// </summary>
public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt, UserResponse User);

public record UserResponse(Guid Id, string Name, string Email, Guid HouseholdId, HouseholdRole HouseholdRole, HouseholdAccessProfile AccessProfile, bool IsAdministrator);
