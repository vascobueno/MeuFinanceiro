using System.Security.Claims;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace MeuFinanceiro.Infrastructure.Services;

/// <summary>UserId/GrupoId/GrupoRole sempre derivados do token JWT validado, nunca do payload do cliente (regra 22).</summary>
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

    public Guid GrupoId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(GrupoClaimTypes.GrupoId);
            return value is not null && Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public GrupoRole GrupoRole
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(GrupoClaimTypes.GrupoRole);
            return value is not null && Enum.TryParse<GrupoRole>(value, out var role) ? role : GrupoRole.Member;
        }
    }

    public GrupoAccessProfile AccessProfile
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(GrupoClaimTypes.AccessProfile);
            return value is not null && Enum.TryParse<GrupoAccessProfile>(value, out var profile) ? profile : GrupoAccessProfile.Completo;
        }
    }

    public bool HasFullAccess => GrupoRole == GrupoRole.Owner || AccessProfile == GrupoAccessProfile.Completo;

    public bool CanWrite => GrupoRole == GrupoRole.Owner || AccessProfile != GrupoAccessProfile.Visualizador;
}
