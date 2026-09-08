namespace MeuFinanceiro.Application.Admin;

/// <summary>Visão de administrador do sistema sobre todos os grupos (independente do próprio).</summary>
public interface IAdminService
{
    Task<IReadOnlyList<AdminGrupoResponse>> GetGruposAsync(CancellationToken ct = default);

    /// <summary>Todos os membros de todos os grupos, achatados numa lista só — visão
    /// cruzada usada pela tela "Perfis de acesso".</summary>
    Task<IReadOnlyList<AdminMemberResponse>> GetAllMembersAsync(CancellationToken ct = default);
}
