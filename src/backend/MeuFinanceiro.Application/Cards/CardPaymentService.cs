using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Cards;

/// <summary>
/// Pagamento de fatura de cartão. Quando sai de uma conta, gera um lançamento nela — é esse
/// lançamento que derruba o saldo, pela consulta de saldo que já existe. Quando é "por outra
/// forma", não há lançamento nenhum: só o pagamento, que abate a fatura.
///
/// O lançamento nasce com Type = CardPayment, nunca Expense: a compra no cartão já é a despesa, e
/// repetir isso no pagamento contaria o mesmo dinheiro duas vezes em todo relatório de gastos.
/// </summary>
public class CardPaymentService : ICardPaymentService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CardPaymentService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CardPaymentResponse> CreateAsync(CreateCardPaymentRequest request, CancellationToken ct = default)
    {
        EnsureCanWrite();

        var card = await FindCardAsync(request.CreditCardId, ct);
        EnsureAmount(request.Amount);

        var cycle = CardInvoiceCycleService.ResolveCycle(card.ClosingDay, card.DueDay, request.CycleReference);
        var account = await FindAccountAsync(request.AccountId, ct);

        var payment = new CardPayment
        {
            GrupoId = _currentUser.GrupoId,
            CreditCardId = card.Id,
            CycleReference = CardInvoiceCycleService.ReferenceOf(cycle),
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            AccountId = account?.Id,
            CreatedBy = _currentUser.UserId,
        };

        if (account is not null)
        {
            var transaction = BuildPaymentTransaction(payment, card.Name, payment.CycleReference);
            _db.FinancialTransactions.Add(transaction);
            payment.PaymentTransactionId = transaction.Id;
        }

        _db.CardPayments.Add(payment);
        await _db.SaveChangesAsync(ct);

        return ToResponse(payment, account?.Name);
    }

    public async Task<CardPaymentResponse> UpdateAsync(Guid id, UpdateCardPaymentRequest request, CancellationToken ct = default)
    {
        EnsureCanWrite();
        EnsureAmount(request.Amount);

        var payment = await FindOwnedAsync(id, ct);
        var card = await FindCardAsync(payment.CreditCardId, ct);
        var account = await FindAccountAsync(request.AccountId, ct);

        var existing = payment.PaymentTransactionId is null
            ? null
            : await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == payment.PaymentTransactionId, ct);

        // Regra 12: lançamento já conciliado não é mexido automaticamente.
        EnsureNotReconciled(existing);

        payment.PaymentDate = request.PaymentDate;
        payment.Amount = request.Amount;
        payment.AccountId = account?.Id;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.UpdatedBy = _currentUser.UserId;

        if (account is null && existing is not null)
        {
            // conta -> outra forma: o dinheiro não saiu de conta nenhuma, então o lançamento some.
            _db.FinancialTransactions.Remove(existing);
            payment.PaymentTransactionId = null;
        }
        else if (account is not null && existing is null)
        {
            // outra forma -> conta: passa a existir o débito.
            var created = BuildPaymentTransaction(payment, card.Name, payment.CycleReference);
            _db.FinancialTransactions.Add(created);
            payment.PaymentTransactionId = created.Id;
        }
        else if (account is not null && existing is not null)
        {
            // conta A -> conta B (ou só valor/data): move o lançamento existente. Os fechamentos
            // de saldo das DUAS contas se invalidam sozinhos — o hook de SaveChangesAsync trata
            // uma entrada Modified considerando o lado original e o atual.
            existing.AccountId = account.Id;
            existing.Amount = payment.Amount;
            existing.TransactionDate = payment.PaymentDate;
            existing.Description = PaymentDescription(card.Name, payment.CycleReference);
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        return ToResponse(payment, account?.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureCanWrite();

        var payment = await FindOwnedAsync(id, ct);

        if (payment.PaymentTransactionId is not null)
        {
            var transaction = await _db.FinancialTransactions
                .FirstOrDefaultAsync(t => t.Id == payment.PaymentTransactionId, ct);

            EnsureNotReconciled(transaction);
            if (transaction is not null) _db.FinancialTransactions.Remove(transaction);
        }

        // A linha "PAGAMENTO" de uma fatura importada pode apontar para este pagamento. Aceita, ela
        // segura a exclusão como o lançamento conciliado segura; só sugerida, volta a "sem par".
        var reconciliations = await _db.Reconciliations.Where(r => r.CardPaymentId == payment.Id).ToListAsync(ct);
        if (reconciliations.Any(r => r.AcceptedAt is not null))
            throw new BusinessRuleException(
                "pagamento-conciliado",
                "Este pagamento já foi conciliado com a fatura importada. Desfaça a conciliação primeiro.");
        foreach (var reconciliation in reconciliations)
        {
            reconciliation.CardPaymentId = null;
            reconciliation.Status = ReconciliationStatus.Unmatched;
            reconciliation.Score = 0;
            reconciliation.Reason = "O pagamento sugerido foi excluído.";
            reconciliation.UpdatedAt = DateTime.UtcNow;
            reconciliation.UpdatedBy = _currentUser.UserId;
        }

        _db.CardPayments.Remove(payment);
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- apoio ----------------

    private static string PaymentDescription(string cardName, DateOnly cycleReference) =>
        $"Pagamento fatura {cardName} {cycleReference:MM/yyyy}";

    /// <summary>
    /// Construído à mão, e não por TransactionService.BuildTransaction, porque aquele fixa
    /// Status = Pending — e a consulta de saldo exclui pendentes, então o pagamento não derrubaria
    /// o saldo, que é metade do pedido.
    ///
    /// Consequência aceita: pagamento com data futura só afeta o saldo quando a data chegar,
    /// porque a consulta de saldo corta em TransactionDate menor ou igual a hoje. É correto.
    /// </summary>
    private FinancialTransaction BuildPaymentTransaction(CardPayment payment, string cardName, DateOnly cycleReference) => new()
    {
        GrupoId = _currentUser.GrupoId,
        TransactionDate = payment.PaymentDate,
        Description = PaymentDescription(cardName, cycleReference),
        AccountId = payment.AccountId,
        CreditCardId = null,
        Type = TransactionType.CardPayment,
        Direction = TransactionDirection.Debit,
        Amount = payment.Amount,
        Status = TransactionStatus.Confirmed,
        CreatedBy = _currentUser.UserId,
    };

    private void EnsureCanWrite()
    {
        if (!_currentUser.CanWrite)
            throw new ForbiddenException("Perfil somente leitura não pode registrar pagamentos.");
    }

    private static void EnsureAmount(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("pagamento-valor-invalido", "O valor do pagamento deve ser maior que zero.");
    }

    private static void EnsureNotReconciled(FinancialTransaction? transaction)
    {
        if (transaction?.Status == TransactionStatus.Reconciled)
            throw new BusinessRuleException(
                "lancamento-conciliado",
                "O lançamento deste pagamento já foi conciliado e não pode ser alterado (regra 12). Desfaça a conciliação primeiro.");
    }

    private async Task<CreditCard> FindCardAsync(Guid cardId, CancellationToken ct) =>
        await _db.CreditCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.GrupoId == _currentUser.GrupoId, ct)
        ?? throw new NotFoundException("Cartão", cardId);

    private async Task<Account?> FindAccountAsync(Guid? accountId, CancellationToken ct)
    {
        if (accountId is null) return null;

        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.GrupoId == _currentUser.GrupoId, ct)
            ?? throw new NotFoundException("Conta", accountId);

        if (!account.IsActive)
            throw new BusinessRuleException("conta-inativa", "Não é possível pagar por uma conta inativa.");

        return account;
    }

    private async Task<CardPayment> FindOwnedAsync(Guid id, CancellationToken ct) =>
        await _db.CardPayments.FirstOrDefaultAsync(p => p.Id == id && p.GrupoId == _currentUser.GrupoId, ct)
        ?? throw new NotFoundException("Pagamento", id);

    private static CardPaymentResponse ToResponse(CardPayment p, string? accountName) => new(
        p.Id, p.CreditCardId, p.CycleReference, p.PaymentDate, p.Amount,
        p.AccountId, accountName, p.PaymentTransactionId);
}
