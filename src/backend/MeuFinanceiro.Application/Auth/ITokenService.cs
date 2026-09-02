using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Auth;

public interface ITokenService
{
    (string token, DateTime expiresAt) GenerateAccessToken(
        Guid userId, string email, string name, Guid grupoId, GrupoRole grupoRole,
        GrupoAccessProfile accessProfile, IEnumerable<string> roles);
    (string token, DateTime expiresAt) GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
