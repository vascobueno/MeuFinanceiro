using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

/// <summary>
/// Monta a fatura de um ciclo somando os lançamentos do cartão dentro da janela do ciclo
/// (CardCycleCalculator) e descontando os pagamentos alocados a ele.
///
/// Não confundir com InvoiceService/CardInvoice: aquilo é o PDF importado do banco, usado só para
/// conciliação, com o total vindo pronto no arquivo. Aqui o total é derivado do que o usuário
/// lançou — é o número que ele controla.
/// </summary>
public class CardInvoiceCycleService : ICardInvoiceCycleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CardInvoiceCycleService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<CardInvoiceCycleResponse> GetAsync(Guid cardId, DateOnly? reference, CancellationToken ct = default)
    {
        var card = await _db.CreditCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.GrupoId == _currentUser.GrupoId, ct)
            ?? throw new NotFoundException("Cartão", cardId);

        var baseCycle = ResolveCycle(card.ClosingDay, card.DueDay, reference);
        var cycleReference = ReferenceOf(baseCycle);
        var cycle = await ApplyClosingOverridesAsync(card, baseCycle, cycleReference, ct);

        // Total do ciclo numa consulta agregada, nunca somando a lista paginada em memória.
        //
        // TODO-NÃO: não filtrar por Status. Os lançamentos nascem Pending (BuildTransaction fixa
        // isso), então excluir pendentes — como o saldo de conta faz — deixaria a fatura zerada
        // para praticamente todo mundo. No cartão, "pendente" significa não conferido, não
        // "ainda não gastei".
        var total = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t => t.CreditCardId == cardId
                     && t.GrupoId == _currentUser.GrupoId
                     && t.TransactionDate >= cycle.Start
                     && t.TransactionDate <= cycle.ClosingDate)
            .SumAsync(t => t.Direction == TransactionDirection.Debit ? t.Amount : -t.Amount, ct);

        var payments = await _db.CardPayments
            .AsNoTracking()
            .Where(p => p.CreditCardId == cardId
                     && p.GrupoId == _currentUser.GrupoId
                     && p.CycleReference == cycleReference)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new CardPaymentResponse(
                p.Id, p.CreditCardId, p.CycleReference, p.PaymentDate, p.Amount,
                p.AccountId, p.Account!.Name, p.PaymentTransactionId))
            .ToListAsync(ct);

        var paid = payments.Sum(p => p.Amount);
        var outstanding = total - paid;   // pode ficar negativo: pagar a mais é crédito, não erro
        var isClosed = cycle.ClosingDate < Today;

        return new CardInvoiceCycleResponse(
            card.Id, card.Name,
            cycleReference,
            cycle.Start, cycle.ClosingDate, cycle.DueDate,
            total, paid, outstanding,
            isClosed,
            IsOverdue: isClosed && cycle.DueDate < Today && outstanding > 0,
            IsClosingOverridden: cycle.ClosingDate != baseCycle.ClosingDate,
            DefaultClosingDate: baseCycle.ClosingDate,
            payments);
    }

    public async Task<CardInvoiceCycleResponse> SetClosingAsync(
        Guid cardId, DateOnly reference, DateOnly closingDate, CancellationToken ct = default)
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode ajustar o fechamento.");

        var card = await FindCardAsync(cardId, ct);
        EnsureReferenceIsMonth(reference);

        // O fechamento tem de ficar DENTRO do próprio mês. Fora dele, dois ciclos se sobreporiam
        // (o mesmo lançamento entraria em duas faturas) ou se inverteriam (janela vazia) — e os
        // dois erram em silêncio, que é o pior jeito de errar dinheiro.
        if (closingDate.Year != reference.Year || closingDate.Month != reference.Month)
            throw new BusinessRuleException(
                "fechamento-fora-do-mes",
                $"O fechamento tem de ser uma data de {reference:MM/yyyy}.");

        var existing = await _db.CardCycleClosings
            .FirstOrDefaultAsync(o => o.CreditCardId == cardId
                                   && o.GrupoId == _currentUser.GrupoId
                                   && o.ReferenceMonth == reference, ct);

        if (existing is null)
        {
            _db.CardCycleClosings.Add(new CardCycleClosing
            {
                GrupoId = _currentUser.GrupoId,
                CreditCardId = card.Id,
                ReferenceMonth = reference,
                ClosingDate = closingDate,
                CreatedBy = _currentUser.UserId,
            });
        }
        else
        {
            existing.ClosingDate = closingDate;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return await GetAsync(cardId, reference, ct);
    }

    public async Task<CardInvoiceCycleResponse> ClearClosingAsync(
        Guid cardId, DateOnly reference, CancellationToken ct = default)
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode ajustar o fechamento.");

        await FindCardAsync(cardId, ct);
        EnsureReferenceIsMonth(reference);

        var existing = await _db.CardCycleClosings
            .FirstOrDefaultAsync(o => o.CreditCardId == cardId
                                   && o.GrupoId == _currentUser.GrupoId
                                   && o.ReferenceMonth == reference, ct);

        // Sem ajuste gravado já é o estado desejado — remover o que não existe não é erro.
        if (existing is not null)
        {
            _db.CardCycleClosings.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        return await GetAsync(cardId, reference, ct);
    }

    private async Task<CreditCard> FindCardAsync(Guid cardId, CancellationToken ct) =>
        await _db.CreditCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.GrupoId == _currentUser.GrupoId, ct)
        ?? throw new NotFoundException("Cartão", cardId);

    private static void EnsureReferenceIsMonth(DateOnly reference)
    {
        if (reference.Day != 1)
            throw new BusinessRuleException(
                "referencia-invalida",
                $"A referência da fatura é o mês (dia 1), e {reference:dd/MM/yyyy} não é.");
    }

    /// <summary>
    /// Aplica as exceções de fechamento (CardCycleClosing) ao ciclo calculado.
    ///
    /// Precisa do mês ANTERIOR também: o início do ciclo é o dia seguinte ao fechamento dele, e se
    /// aquele mês tiver exceção, a janela deste muda junto. Os dois vêm numa consulta só.
    ///
    /// O vencimento é recalculado a partir do fechamento efetivo — se a operadora antecipa o
    /// fechamento, o vencimento acompanha. A regra vem de CardCycleCalculator.DueDateFor, para
    /// não existir uma segunda cópia dela aqui.
    /// </summary>
    private async Task<CardCycle> ApplyClosingOverridesAsync(
        CreditCard card, CardCycle baseCycle, DateOnly cycleReference, CancellationToken ct)
    {
        var previousReference = cycleReference.AddMonths(-1);

        var overrides = await _db.CardCycleClosings
            .AsNoTracking()
            .Where(o => o.CreditCardId == card.Id
                     && o.GrupoId == _currentUser.GrupoId
                     && (o.ReferenceMonth == cycleReference || o.ReferenceMonth == previousReference))
            .ToDictionaryAsync(o => o.ReferenceMonth, o => o.ClosingDate, ct);

        if (overrides.Count == 0) return baseCycle;

        var closing = overrides.GetValueOrDefault(cycleReference, baseCycle.ClosingDate);

        var previousBase = CardCycleCalculator.CycleForMonth(card.ClosingDay, card.DueDay, previousReference);
        var previousClosing = overrides.GetValueOrDefault(previousReference, previousBase.ClosingDate);

        return new CardCycle(
            previousClosing.AddDays(1),
            closing,
            CardCycleCalculator.DueDateFor(closing, card.DueDay));
    }

    /// <summary>
    /// O mês de referência de um ciclo — a sua identidade. Derivado do mês do fechamento, que por
    /// construção (e por invariante, quando ajustado) cai sempre dentro do próprio mês.
    /// </summary>
    internal static DateOnly ReferenceOf(CardCycle cycle) =>
        new(cycle.ClosingDate.Year, cycle.ClosingDate.Month, 1);

    /// <summary>
    /// Resolve o ciclo pedido. A referência é o MÊS (dia 1), não a data de fechamento: o
    /// fechamento pode ser ajustado por fatura, e usá-lo como chave deixaria os pagamentos já
    /// registrados órfãos a cada ajuste.
    /// </summary>
    internal static CardCycle ResolveCycle(int closingDay, int dueDay, DateOnly? reference)
    {
        if (reference is null)
            return CardCycleCalculator.CycleOf(closingDay, dueDay, DateOnly.FromDateTime(DateTime.UtcNow));

        if (reference.Value.Day != 1)
            throw new BusinessRuleException(
                "referencia-invalida",
                $"A referência da fatura é o mês (dia 1), e {reference:dd/MM/yyyy} não é.");

        return CardCycleCalculator.CycleForMonth(closingDay, dueDay, reference.Value);
    }
}
