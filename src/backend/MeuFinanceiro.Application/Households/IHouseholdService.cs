using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Households;

/// <summary>
/// Provisionamento e gestão de membros de um household. Autorização (Owner-only nas rotas
/// /me, Administrator nas rotas /admin) é feita pelos controllers antes de chamar este
/// serviço — os métodos aqui não fazem checagem de papel, só a operação de dados.
/// </summary>
public interface IHouseholdService
{
    /// <summary>Chamado durante o registro (regra: usuário nunca existe sem household).</summary>
    Task<Guid> CreateForNewUserAsync(Guid userId, string userName, CancellationToken ct = default);

    Task<HouseholdResponse> GetAsync(Guid householdId, CancellationToken ct = default);

    /// <summary>
    /// Só permite mover o convidado se o household dele ainda estiver intocado (sem outros
    /// membros, sem dados próprios) — senão recusa (regra confirmada com o usuário).
    /// </summary>
    Task<HouseholdMemberResponse> AddMemberAsync(Guid householdId, AddMemberRequest request, CancellationToken ct = default);

    /// <summary>
    /// O removido nunca fica sem household — ganha um novo, vazio, do qual é Owner (mesma
    /// garantia do registro). Bloqueia remover o último Owner.
    /// </summary>
    Task RemoveMemberAsync(Guid householdId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Owner nunca pode ser rebaixado (rejeita com BusinessRuleException) — só Members podem
    /// ter o AccessProfile alterado.
    /// </summary>
    Task UpdateMemberProfileAsync(Guid householdId, Guid userId, HouseholdAccessProfile profile, CancellationToken ct = default);
}
