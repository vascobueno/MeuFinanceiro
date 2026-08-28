using System.Security.Claims;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>UserId/HouseholdId/HouseholdRole sempre derivados do token JWT validado, nunca do payload do cliente (regra 22).</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdministrator => _httpContextAccessor.HttpContext?.User?.IsInRole("Administrator") ?? false;

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return value is not null && Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid HouseholdId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(HouseholdClaimTypes.HouseholdId);
            return value is not null && Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public HouseholdRole HouseholdRole
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(HouseholdClaimTypes.HouseholdRole);
            return value is not null && Enum.TryParse<HouseholdRole>(value, out var role) ? role : HouseholdRole.Member;
        }
    }

    public HouseholdAccessProfile AccessProfile
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(HouseholdClaimTypes.AccessProfile);
            return value is not null && Enum.TryParse<HouseholdAccessProfile>(value, out var profile) ? profile : HouseholdAccessProfile.Completo;
        }
    }

    public bool HasFullAccess => HouseholdRole == HouseholdRole.Owner || AccessProfile == HouseholdAccessProfile.Completo;

    public bool CanWrite => HouseholdRole == HouseholdRole.Owner || AccessProfile != HouseholdAccessProfile.Visualizador;
}
