using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Accounts;

/// <summary>
/// Saldo de conta em uma única consulta agrupada. Substitui o N+1 do AccountService, que fazia
/// uma soma por conta dentro de um laço — medido em 12 consultas para 10 contas.
/// </summary>
public class AccountBalanceService : IAccountBalanceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AccountBalanceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default)
    {
        if (accountIds.Count == 0) return new Dictionary<Guid, decimal>();

        var rows = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId!.Value))
            .ToAccountMovements(Today)
            .ToListAsync(ct);

        var byAccount = rows.ToDictionary(r => r.AccountId, r => r.Total);

        // Conta sem lançamento não aparece no GROUP BY — completa com zero para o chamador não
        // precisar tratar ausência.
        return accountIds.ToDictionary(id => id, id => byAccount.GetValueOrDefault(id, 0m));
    }

    public async Task<decimal> GetGroupMovementsTotalAsync(CancellationToken ct = default)
    {
        var rows = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.GrupoId == _currentUser.GrupoId)
            .ToAccountMovements(Today)
            .ToListAsync(ct);

        return rows.Sum(r => r.Total);
    }
}
