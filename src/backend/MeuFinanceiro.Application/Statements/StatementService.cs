using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Statements;

/// <summary>
/// Tela Extratos: lista os extratos bancários importados, com filtros, ordenação e paginação
/// server-side (regra 19). É a irmã de <see cref="Invoices.InvoiceService"/> do lado da conta.
///
/// Só lê: quem grava extrato é a importação da conciliação bancária
/// (docs/09-conciliacao-conta-corrente.md).
/// </summary>
public class StatementService : IStatementService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StatementService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<BankStatementListItemResponse>> GetAllAsync(BankStatementFilter filter, CancellationToken ct = default)
    {
        // O GrupoId é filtrado à mão porque não há HasQueryFilter global no AppDbContext:
        // esquecer esta linha vazaria extrato de outra família. Há teste dedicado para isso.
        var query = _db.BankStatements
            .AsNoTracking()
            .Include(s => s.Account)
            .Where(s => s.GrupoId == _currentUser.GrupoId);

        if (filter.AccountId is not null) query = query.Where(s => s.AccountId == filter.AccountId);
        // O período filtra pelo FIM do extrato: é a data que diz a que mês ele pertence.
        if (filter.PeriodStart is not null) query = query.Where(s => s.ReferenceEnd >= filter.PeriodStart);
        if (filter.PeriodEnd is not null) query = query.Where(s => s.ReferenceEnd <= filter.PeriodEnd);
        if (filter.MinAmount is not null) query = query.Where(s => s.ClosingBalance >= filter.MinAmount);
        if (filter.MaxAmount is not null) query = query.Where(s => s.ClosingBalance <= filter.MaxAmount);

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "conta" => filter.IsDescending ? query.OrderByDescending(s => s.Account!.Name) : query.OrderBy(s => s.Account!.Name),
            "periodo" => filter.IsDescending ? query.OrderByDescending(s => s.ReferenceEnd) : query.OrderBy(s => s.ReferenceEnd),
            "saldofinal" => filter.IsDescending ? query.OrderByDescending(s => s.ClosingBalance) : query.OrderBy(s => s.ClosingBalance),
            "movimento" => filter.IsDescending
                ? query.OrderByDescending(s => s.ClosingBalance - s.OpeningBalance)
                : query.OrderBy(s => s.ClosingBalance - s.OpeningBalance),
            _ => filter.IsDescending ? query.OrderByDescending(s => s.ReferenceEnd) : query.OrderBy(s => s.ReferenceEnd)
        };

        var totalItems = await query.CountAsync(ct);

        // A contagem de itens vai na projeção, e não por Include/Count em memória: com Include
        // seria uma consulta por linha (N+1) só para mostrar um número.
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new BankStatementListItemResponse(
                s.Id,
                s.AccountId,
                s.Account!.Name,
                s.ReferenceStart,
                s.ReferenceEnd,
                s.OpeningBalance,
                s.ClosingBalance,
                s.ClosingBalance - s.OpeningBalance,
                s.Items.Count,
                s.OriginalFileName))
            .ToListAsync(ct);

        return PagedResult<BankStatementListItemResponse>.Create(items, filter.Page, filter.PageSize, totalItems);
    }
}
