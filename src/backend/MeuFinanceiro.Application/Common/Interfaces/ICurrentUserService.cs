using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Common.Interfaces;

/// <summary>
/// UserId/GrupoId/GrupoRole derivados da identidade (token JWT), nunca confiados do
/// cliente (regra 22, docs/12-seguranca.md).
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid GrupoId { get; }
    GrupoRole GrupoRole { get; }
    GrupoAccessProfile AccessProfile { get; }
    bool IsAuthenticated { get; }
    bool IsAdministrator { get; }

    /// <summary>Vê páginas de gestão do grupo (Configurações/Membros) — Owner sempre, ou
    /// Member com perfil Completo.</summary>
    bool HasFullAccess { get; }

    /// <summary>Pode criar/editar/excluir dados financeiros — todos menos Visualizador.</summary>
    bool CanWrite { get; }
}

/// <summary>Nomes de claim custom do JWT sem equivalente em <see cref="System.Security.Claims.ClaimTypes"/>.</summary>
public static class GrupoClaimTypes
{
    public const string GrupoId = "grupo_id";
    public const string GrupoRole = "grupo_role";
    public const string AccessProfile = "grupo_access_profile";
}
