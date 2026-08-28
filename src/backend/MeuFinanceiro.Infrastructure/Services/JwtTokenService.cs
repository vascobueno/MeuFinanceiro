using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MeuFinanceiro.Application.Auth;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>
/// Access token de vida curta (15 min, configurável) + refresh token opaco (7 dias),
/// armazenado com hash no banco — docs/12-seguranca.md.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string token, DateTime expiresAt) GenerateAccessToken(
        Guid userId, string email, string name, Guid householdId, HouseholdRole householdRole,
        HouseholdAccessProfile accessProfile, IEnumerable<string> roles)
    {
        // Owner é sempre Completo, independente do que estiver salvo — defesa em profundidade
        // além da checagem em HouseholdService.UpdateMemberProfileAsync (nunca autotrancar o Owner).
        if (householdRole == HouseholdRole.Owner)
            accessProfile = HouseholdAccessProfile.Completo;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = _configuration.GetValue<int?>("Jwt:AccessTokenExpirationMinutes") ?? 15;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, name),
            new(HouseholdClaimTypes.HouseholdId, householdId.ToString()),
            new(HouseholdClaimTypes.HouseholdRole, householdRole.ToString()),
            new(HouseholdClaimTypes.AccessProfile, accessProfile.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public (string token, DateTime expiresAt) GenerateRefreshToken()
    {
        var expirationDays = _configuration.GetValue<int?>("Jwt:RefreshTokenExpirationDays") ?? 7;
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return (Convert.ToBase64String(randomBytes), DateTime.UtcNow.AddDays(expirationDays));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }
}
