using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

public class CardService : ICardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CardService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<CardResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.CreditCards.AsNoTracking().Where(c => c.GrupoId == _currentUser.GrupoId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ToLower() dos dois lados: ver comentário equivalente em MerchantService.
            var normalizedSearch = request.Search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(normalizedSearch));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "brand" => request.IsDescending ? query.OrderByDescending(c => c.Brand) : query.OrderBy(c => c.Brand),
            "closingday" => request.IsDescending ? query.OrderByDescending(c => c.ClosingDay) : query.OrderBy(c => c.ClosingDay),
            "dueday" => request.IsDescending ? query.OrderByDescending(c => c.DueDay) : query.OrderBy(c => c.DueDay),
            "limit" => request.IsDescending ? query.OrderByDescending(c => c.Limit) : query.OrderBy(c => c.Limit),
            _ => request.IsDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name)
        };

        var totalItems = await query.CountAsync(ct);
        var cards = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        return PagedResult<CardResponse>.Create(cards.Select(ToResponse).ToList(), request.Page, request.PageSize, totalItems);
    }

    public async Task<CardResponse> GetByIdAsync(Guid id, CancellationToken ct = default) => ToResponse(await FindOwnedAsync(id, ct));

    public async Task<CardResponse> CreateAsync(CreateCardRequest request, CancellationToken ct = default)
    {
        var card = new CreditCard
        {
            GrupoId = _currentUser.GrupoId,
            Name = request.Name,
            Brand = CardBrands.ParseForCreditGroup(request.Brand),
            ClosingDay = request.ClosingDay,
            DueDay = request.DueDay,
            Limit = request.Limit,
            CreatedBy = _currentUser.UserId
        };

        _db.CreditCards.Add(card);
        await _db.SaveChangesAsync(ct);
        return ToResponse(card);
    }

    public async Task<CardResponse> UpdateAsync(Guid id, UpdateCardRequest request, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);
        card.Name = request.Name;
        card.Brand = CardBrands.ParseForCreditGroup(request.Brand);
        card.ClosingDay = request.ClosingDay;
        card.DueDay = request.DueDay;
        card.Limit = request.Limit;
        card.IsActive = request.IsActive;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToResponse(card);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var card = await FindOwnedAsync(id, ct);

        var hasTransactions = await _db.FinancialTransactions.AnyAsync(t => t.CreditCardId == id, ct);
        if (hasTransactions)
            throw new BusinessRuleException("cartao-possui-lancamentos", "Não é possível excluir um cartão que já possui lançamentos. Desative-o em vez de excluir.");

        // Pagamento de fatura tem FK Restrict para o cartão: sem esta checagem o erro viria do
        // banco, cru, em vez da mensagem que a tela sabe mostrar.
        var hasPayments = await _db.CardPayments.AnyAsync(p => p.CreditCardId == id, ct);
        if (hasPayments)
            throw new BusinessRuleException("cartao-possui-pagamentos", "Não é possível excluir um cartão que já possui pagamentos de fatura. Desative-o em vez de excluir.");

        // Cartões do grupo: os só de crédito vão junto; o múltiplo continua como cartão de débito,
        // e leva a bandeira do grupo — sem ela, ficaria um cartão de débito sem bandeira.
        await DetachOrRemoveIssuedCardsAsync(_db, _db.IssuedCards.Where(c => c.CreditCardId == id),
            c => c.AccountId is not null, c => { c.CreditCardId = null; c.Brand = card.Brand; }, ct);

        _db.CreditCards.Remove(card);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Excluir um lado (grupo de crédito ou conta) não pode levar o múltiplo junto: ele perde só
    /// aquela função. Os cartões que só tinham aquela função saem. Usado também por AccountService.
    /// </summary>
    internal static async Task DetachOrRemoveIssuedCardsAsync(IApplicationDbContext db, IQueryable<IssuedCard> cards,
        Func<IssuedCard, bool> keeps, Action<IssuedCard> detach, CancellationToken ct)
    {
        var list = await cards.ToListAsync(ct);
        var removed = list.Where(c => !keeps(c)).Select(c => c.Id).ToHashSet();
        if (removed.Count > 0)
        {
            // Substituídos que apontam para um cartão que sai deixam de apontar (FK Restrict).
            var pointing = await db.IssuedCards.Where(c => c.ReplacedById != null && removed.Contains(c.ReplacedById.Value)).ToListAsync(ct);
            foreach (var c in pointing) c.ReplacedById = null;
        }
        foreach (var c in list)
        {
            if (keeps(c)) detach(c);
            else db.IssuedCards.Remove(c);
        }
    }

    private async Task<CreditCard> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException(nameof(CreditCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return card;
    }

    private static CardResponse ToResponse(CreditCard c) => new(c.Id, c.Name, c.Brand?.ToString(), c.ClosingDay, c.DueDay, c.Limit, c.IsActive);
}
