using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Accounts;

/// <summary>
/// Saldo de conta a partir do último fechamento mensal + movimentos posteriores.
///
/// O fechamento é cache derivado: a verdade continua sendo a soma dos lançamentos. Se a linha de
/// fechamento não existir, o cálculo cai no caminho completo e devolve o mesmo número — só mais
/// devagar. Ou seja, fechamento **ausente** nunca dá saldo errado; fechamento **defasado** daria,
/// e é por isso que a invalidação vive no SaveChangesAsync (AppDbContext), onde nenhum caminho de
/// escrita pelo EF escapa.
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

    /// <summary>
    /// Âncora fixa: último dia do mês anterior. Ser fixa (e não "o fechamento mais recente de cada
    /// conta") é o que permite buscar os fechamentos da página inteira com um único WHERE, sem
    /// subconsulta correlacionada por conta.
    /// </summary>
    private static DateOnly AnchorFor(DateOnly today) => new DateOnly(today.Year, today.Month, 1).AddDays(-1);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default)
    {
        if (accountIds.Count == 0) return new Dictionary<Guid, decimal>();

        var today = Today;
        var anchor = AnchorFor(today);

        var closings = await _db.AccountMonthlyBalances
            .AsNoTracking()
            .Where(b => accountIds.Contains(b.AccountId) && b.PeriodEnd == anchor)
            .ToDictionaryAsync(b => b.AccountId, b => b.CumulativeAmount, ct);

        var missing = accountIds.Where(id => !closings.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var generated = await GenerateClosingsAsync(missing, anchor, ct);
            foreach (var (id, total) in generated) closings[id] = total;
        }

        // Só o que veio depois da âncora: no máximo o mês corrente, em vez do histórico inteiro.
        var afterAnchor = await _db.FinancialTransactions
            .AsNoTracking()
            // A guarda de nulo precisa vir aqui, e não só dentro de ToAccountMovements: este Where
            // é avaliado ANTES do de lá, então `.Value` estouraria em lançamento de cartão.
            .Where(t => t.AccountId != null && accountIds.Contains(t.AccountId.Value) && t.TransactionDate > anchor)
            .ToAccountMovements(today)
            .ToListAsync(ct);

        var recent = afterAnchor.ToDictionary(r => r.AccountId, r => r.Total);

        return accountIds.ToDictionary(
            id => id,
            id => closings.GetValueOrDefault(id, 0m) + recent.GetValueOrDefault(id, 0m));
    }

    public async Task<decimal> GetGroupMovementsTotalAsync(CancellationToken ct = default)
    {
        var accountIds = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.GrupoId == _currentUser.GrupoId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var balances = await GetBalancesAsync(accountIds, ct);
        return balances.Values.Sum();
    }

    public async Task<IReadOnlyList<AccountBalanceCheck>> VerifyAsync(CancellationToken ct = default)
    {
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => a.GrupoId == _currentUser.GrupoId)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync(ct);

        var ids = accounts.Select(a => a.Id).ToList();
        var viaFechamento = await GetBalancesAsync(ids, ct);

        // Caminho independente e deliberadamente ingênuo: soma tudo direto dos lançamentos, sem
        // olhar para o cache. É o oráculo — se os dois discordam, quem está errado é o cache.
        var viaSomaBruta = (await _db.FinancialTransactions
                .AsNoTracking()
                .Where(t => t.AccountId != null && ids.Contains(t.AccountId.Value))
                .ToAccountMovements(Today)
                .ToListAsync(ct))
            .ToDictionary(r => r.AccountId, r => r.Total);

        return accounts
            .Select(a => new AccountBalanceCheck(
                a.Id,
                a.Name,
                viaFechamento.GetValueOrDefault(a.Id, 0m),
                viaSomaBruta.GetValueOrDefault(a.Id, 0m)))
            .ToList();
    }

    /// <summary>
    /// Gera os fechamentos que faltam, acumulando mês a mês a partir da última âncora existente
    /// (ou do início, se não houver). O valor devolvido é calculado ANTES de persistir e não
    /// depende do commit: se a inserção falhar — por corrida entre requisições, que o índice
    /// único barra — o saldo ainda sai correto, só não fica em cache desta vez.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GenerateClosingsAsync(
        IReadOnlyCollection<Guid> accountIds,
        DateOnly anchor,
        CancellationToken ct)
    {
        var anchors = await _db.AccountMonthlyBalances
            .AsNoTracking()
            .Where(b => accountIds.Contains(b.AccountId) && b.IsAnchor && b.PeriodEnd <= anchor)
            .GroupBy(b => b.AccountId)
            .Select(g => g.OrderByDescending(b => b.PeriodEnd).First())
            .ToDictionaryAsync(b => b.AccountId, b => b, ct);

        // Movimento por conta e por mês, do ponto de partida até a âncora.
        var monthly = await _db.FinancialTransactions
            .AsNoTracking()
            // O `t.AccountId != null` vem PRIMEIRO de propósito: sem ele, `.Value` estoura em
            // lançamento de cartão (que não tem conta) assim que a consulta é avaliada em memória.
            // É a mesma guarda que ToAccountMovements já fazia no caminho de leitura — faltava aqui.
            .Where(t => t.AccountId != null
                     && accountIds.Contains(t.AccountId.Value)
                     && t.Status != Domain.Enums.TransactionStatus.Pending
                     && t.TransactionDate <= anchor)
            .GroupBy(t => new { AccountId = t.AccountId!.Value, t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Year,
                g.Key.Month,
                Total = g.Sum(t => t.Direction == Domain.Enums.TransactionDirection.Credit ? t.Amount : -t.Amount)
            })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, decimal>();

        foreach (var accountId in accountIds)
        {
            anchors.TryGetValue(accountId, out var anchorRow);
            var startExclusive = anchorRow?.PeriodEnd;

            var months = monthly
                .Where(m => m.AccountId == accountId)
                .Select(m => new
                {
                    PeriodEnd = new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)),
                    m.Total
                })
                .Where(m => startExclusive is null || m.PeriodEnd > startExclusive)
                .OrderBy(m => m.PeriodEnd)
                .ToList();

            var cumulative = anchorRow?.CumulativeAmount ?? 0m;
            var grupoId = anchorRow?.GrupoId ?? _currentUser.GrupoId;

            foreach (var month in months)
            {
                cumulative += month.Total;
                _db.AccountMonthlyBalances.Add(new AccountMonthlyBalance
                {
                    GrupoId = grupoId,
                    AccountId = accountId,
                    PeriodEnd = month.PeriodEnd,
                    MonthAmount = month.Total,
                    CumulativeAmount = cumulative,
                    CreatedBy = _currentUser.UserId
                });
            }

            // A âncora do mês corrente precisa existir mesmo que não tenha havido movimento no mês,
            // senão a próxima leitura regenera tudo de novo.
            if (months.Count == 0 || months[^1].PeriodEnd < anchor)
            {
                _db.AccountMonthlyBalances.Add(new AccountMonthlyBalance
                {
                    GrupoId = grupoId,
                    AccountId = accountId,
                    PeriodEnd = anchor,
                    MonthAmount = 0m,
                    CumulativeAmount = cumulative,
                    CreatedBy = _currentUser.UserId
                });
            }

            result[accountId] = cumulative;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outra requisição gerou os mesmos fechamentos primeiro (índice único). O valor já foi
            // calculado acima e continua correto — só não ficou em cache nesta passagem.
        }

        return result;
    }
}
