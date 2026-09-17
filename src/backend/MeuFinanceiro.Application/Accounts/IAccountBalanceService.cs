namespace MeuFinanceiro.Application.Accounts;

public interface IAccountBalanceService
{
    /// <summary>
    /// Saldo atual (até hoje) de várias contas de uma vez. Recebe a página inteira e resolve em
    /// uma única consulta agrupada — antes era uma soma por conta, dentro de um laço.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct = default);

    /// <summary>Soma de todas as contas do grupo até hoje, sem o saldo inicial.</summary>
    Task<decimal> GetGroupMovementsTotalAsync(CancellationToken ct = default);
}
