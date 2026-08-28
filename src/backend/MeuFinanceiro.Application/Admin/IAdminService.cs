namespace MeuFinanceiro.Application.Admin;

/// <summary>Visão de administrador do sistema sobre todos os households (independente do próprio).</summary>
public interface IAdminService
{
    Task<IReadOnlyList<AdminHouseholdResponse>> GetHouseholdsAsync(CancellationToken ct = default);

    /// <summary>Todos os membros de todos os households, achatados numa lista só — visão
    /// cruzada usada pela tela "Perfis de acesso".</summary>
    Task<IReadOnlyList<AdminMemberResponse>> GetAllMembersAsync(CancellationToken ct = default);
}
