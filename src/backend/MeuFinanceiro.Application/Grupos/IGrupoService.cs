using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Grupos;

/// <summary>
/// Provisionamento e gestão de membros de um grupo. Autorização (Owner-only nas rotas
/// /me, Administrator nas rotas /admin) é feita pelos controllers antes de chamar este
/// serviço — os métodos aqui não fazem checagem de papel, só a operação de dados.
/// </summary>
public interface IGrupoService
{
    /// <summary>Chamado durante o registro (regra: usuário nunca existe sem grupo).</summary>
    Task<Guid> CreateForNewUserAsync(Guid userId, string userName, CancellationToken ct = default);

    Task<GrupoResponse> GetAsync(Guid grupoId, CancellationToken ct = default);

    /// <summary>
    /// Só permite mover o convidado se o grupo dele ainda estiver intocado (sem outros
    /// membros, sem dados próprios) — senão recusa (regra confirmada com o usuário).
    /// </summary>
    Task<GrupoMemberResponse> AddMemberAsync(Guid grupoId, AddMemberRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cria a conta inteira de alguém que ainda não tem conta, com senha temporária enviada
    /// por e-mail — diferente de AddMemberAsync (que só move quem já tem conta própria).
    /// </summary>
    Task<GrupoMemberResponse> CreateMemberAsync(Guid grupoId, CreateMemberRequest request, CancellationToken ct = default);

    /// <summary>
    /// O removido nunca fica sem grupo — ganha um novo, vazio, do qual é Owner (mesma
    /// garantia do registro). Bloqueia remover o último Owner.
    /// </summary>
    Task RemoveMemberAsync(Guid grupoId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Owner nunca pode ser rebaixado (rejeita com BusinessRuleException) — só Members podem
    /// ter o AccessProfile alterado.
    /// </summary>
    Task UpdateMemberProfileAsync(Guid grupoId, Guid userId, GrupoAccessProfile profile, CancellationToken ct = default);
}
