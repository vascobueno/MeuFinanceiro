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

        var payments = await _db.CardPayments
            .AsNoTracking()
            .Where(p => p.CreditCardId == cardId
                     && p.GrupoId == _currentUser.GrupoId
                     && p.CycleClosingDate == cycle.ClosingDate)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new CardPaymentResponse(
                p.Id, p.CreditCardId, p.CycleClosingDate, p.PaymentDate, p.Amount,
                p.AccountId, p.Account!.Name, p.PaymentTransactionId))
            .ToListAsync(ct);

        var paid = payments.Sum(p => p.Amount);
        var outstanding = total - paid;   // pode ficar negativo: pagar a mais é crédito, não erro
        var isClosed = cycle.ClosingDate < Today;

        return new CardInvoiceCycleResponse(
            card.Id, card.Name,
            cycle.Start, cycle.ClosingDate, cycle.DueDate,
            total, paid, outstanding,
            isClosed,
            IsOverdue: isClosed && cycle.DueDate < Today && outstanding > 0,
            payments);
    }

    /// <summary>
    /// Resolve o ciclo pedido. Uma referência que não seja exatamente uma data de fechamento do
    /// cartão é recusada — aceitá-la deixaria pagamentos alocados a um ciclo que não existe, e
    /// eles sumiriam da tela sem explicação.
    /// </summary>
    internal static CardCycle ResolveCycle(int closingDay, int dueDay, DateOnly? reference)
    {
        if (reference is null)
            return CardCycleCalculator.CycleOf(closingDay, dueDay, DateOnly.FromDateTime(DateTime.UtcNow));

        var cycle = CardCycleCalculator.CycleOf(closingDay, dueDay, reference.Value);
        if (cycle.ClosingDate != reference.Value)
            throw new BusinessRuleException(
                "ciclo-invalido",
                $"{reference:dd/MM/yyyy} não é uma data de fechamento deste cartão.");

        return cycle;
    }
}
