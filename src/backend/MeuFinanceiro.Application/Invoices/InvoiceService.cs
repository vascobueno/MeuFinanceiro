using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Invoices;

/// <summary>Tela Faturas (docs/screens/faturas): listar faturas de cartão com filtros/ordenação/paginação server-side (regra 19).</summary>
public class InvoiceService : IInvoiceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InvoiceService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<CardInvoiceListItemResponse>> GetAllAsync(CardInvoiceFilter filter, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.CardInvoices
            .AsNoTracking()
            .Include(i => i.CreditCard)
            .Where(i => i.GrupoId == _currentUser.GrupoId);

        if (filter.CreditCardId is not null) query = query.Where(i => i.CreditCardId == filter.CreditCardId);
        if (filter.PeriodStart is not null) query = query.Where(i => i.ClosingDate >= filter.PeriodStart);
        if (filter.PeriodEnd is not null) query = query.Where(i => i.ClosingDate <= filter.PeriodEnd);
        if (filter.MinAmount is not null) query = query.Where(i => i.TotalAmount >= filter.MinAmount);
        if (filter.MaxAmount is not null) query = query.Where(i => i.TotalAmount <= filter.MaxAmount);

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<CardInvoiceStatus>(filter.Status, true, out var status))
        {
            query = status == CardInvoiceStatus.Aberta
                ? query.Where(i => i.DueDate >= today)
                : query.Where(i => i.DueDate < today);
        }

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "cartao" => filter.IsDescending ? query.OrderByDescending(i => i.CreditCard!.Name) : query.OrderBy(i => i.CreditCard!.Name),
            "fechamento" => filter.IsDescending ? query.OrderByDescending(i => i.ClosingDate) : query.OrderBy(i => i.ClosingDate),
            "vencimento" => filter.IsDescending ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            "total" => filter.IsDescending ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            _ => filter.IsDescending ? query.OrderByDescending(i => i.ClosingDate) : query.OrderBy(i => i.ClosingDate)
        };

        var totalItems = await query.CountAsync(ct);
        var results = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);

        var items = results.Select(i => new CardInvoiceListItemResponse(
            i.Id,
            i.CreditCardId,
            i.CreditCard!.Name,
            i.ClosingDate,
            i.DueDate,
            i.TotalAmount,
            i.DueDate >= today ? CardInvoiceStatus.Aberta : CardInvoiceStatus.Vencida
        )).ToList();

        return PagedResult<CardInvoiceListItemResponse>.Create(items, filter.Page, filter.PageSize, totalItems);
    }
}
