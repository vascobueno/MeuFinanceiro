using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Recurring;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Projection;

/// <summary>
/// docs/11-projecao-financeira.md: projeta contas (lançamentos futuros já registrados +
/// recorrências ainda não materializadas + parcelas) e investimentos (somente posição atual,
/// sem inventar rentabilidade). Patrimônio consolidado é só para apresentação (regra 8).
/// </summary>
public class ProjectionService : IProjectionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ProjectionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProjectionResponse> GetProjectionAsync(DateOnly date, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var accounts = await _db.Accounts.AsNoTracking().Where(a => a.GrupoId == _currentUser.GrupoId).ToListAsync(ct);
        var initialBalance = accounts.Sum(a => a.InitialBalance);

        var recordedTransactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.GrupoId == _currentUser.GrupoId && t.AccountId != null && t.Status != TransactionStatus.Pending)
            .Select(t => new { t.TransactionDate, t.Description, t.Amount, t.Direction, t.Type })
            .ToListAsync(ct);

        var currentAccountsBalance = initialBalance + recordedTransactions
            .Where(t => t.TransactionDate <= today)
            .Sum(t => t.Direction == TransactionDirection.Credit ? t.Amount : -t.Amount);

        var timeline = new List<ProjectionEntry>();

        // 1) Lançamentos futuros já registrados (parcelas futuras, primeira ocorrência de recorrência etc.).
        foreach (var t in recordedTransactions.Where(t => t.TransactionDate > today && t.TransactionDate <= date))
        {
            var signedAmount = t.Direction == TransactionDirection.Credit ? t.Amount : -t.Amount;
            timeline.Add(new ProjectionEntry(t.TransactionDate, t.Description, signedAmount, IsProjected: false, t.Type));
        }

        // 2) Ocorrências futuras de recorrências ainda não materializadas em lançamentos (regra 13:
        //    usa sempre a versão vigente para a data da ocorrência).
        if (date > today)
        {
            var recurrences = await _db.RecurringTransactions
                .AsNoTracking()
                .Include(r => r.Versions)
                .Where(r => r.GrupoId == _currentUser.GrupoId && r.IsActive && r.AccountId != null)
                .ToListAsync(ct);

            foreach (var recurrence in recurrences)
            {
                foreach (var occurrenceDate in RecurrenceOccurrenceCalculator.EnumerateUntil(recurrence, today, date))
                {
                    var version = recurrence.Versions
                        .Where(v => v.EffectiveFrom <= occurrenceDate)
                        .OrderByDescending(v => v.EffectiveFrom)
                        .FirstOrDefault();

                    if (version is null) continue;

                    var signedAmount = recurrence.Type == TransactionType.Income ? version.Amount : -version.Amount;
                    timeline.Add(new ProjectionEntry(occurrenceDate, $"{recurrence.Description} (recorrência projetada)", signedAmount, IsProjected: true, recurrence.Type));
                }
            }
        }

        timeline = timeline.OrderBy(e => e.Date).ToList();

        var projectedAccountsBalance = currentAccountsBalance + timeline.Sum(e => e.Amount);

        // Alerta de saldo negativo (docs/11): caminha cronologicamente a partir do saldo atual.
        var running = currentAccountsBalance;
        var hasNegative = running < 0;
        foreach (var entry in timeline)
        {
            running += entry.Amount;
            if (running < 0) hasNegative = true;
        }

        // Investimentos: apenas posição atual, nunca projeção de rentabilidade (docs/11).
        var investments = await _db.Investments
            .AsNoTracking()
            .Include(i => i.Positions)
            .Include(i => i.InvestmentAccount)
            .Where(i => i.InvestmentAccount!.GrupoId == _currentUser.GrupoId)
            .ToListAsync(ct);

        var investmentsBalance = investments
            .Select(i => i.Positions.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt).FirstOrDefault())
            .Where(p => p is not null)
            .Sum(p => p!.NetValue);

        return new ProjectionResponse(
            date,
            currentAccountsBalance,
            projectedAccountsBalance,
            investmentsBalance,
            projectedAccountsBalance + investmentsBalance,
            hasNegative,
            timeline
        );
    }

}
