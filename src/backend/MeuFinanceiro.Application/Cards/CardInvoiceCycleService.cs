using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
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

        var cycle = ResolveCycle(card.ClosingDay, card.DueDay, reference);

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

        var cycleReference = ReferenceOf(cycle);
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
            payments);
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
