using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Auth;

public interface ITokenService
{
    (string token, DateTime expiresAt) GenerateAccessToken(
        Guid userId, string email, string name, Guid householdId, HouseholdRole householdRole,
        HouseholdAccessProfile accessProfile, IEnumerable<string> roles);
    (string token, DateTime expiresAt) GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
