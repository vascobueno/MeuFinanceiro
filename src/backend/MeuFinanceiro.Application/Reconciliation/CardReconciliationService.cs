using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>docs/08-conciliacao-cartoes.md — entrada normalizada da fatura (PDF fica a cargo de um parser futuro).</summary>
public class CardReconciliationService : ICardReconciliationService
{
    private const int SearchWindowDays = 15; // faturas costumam ter maior distância entre compra e lançamento

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public CardReconciliationService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<CardInvoiceResponse> ImportAsync(ImportCardInvoiceRequest request, CancellationToken ct = default)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == request.CreditCardId, ct)
            ?? throw new NotFoundException(nameof(CreditCard), request.CreditCardId);
        if (card.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var itemsSum = request.Items.Sum(i => i.Amount);
        var totalValidated = itemsSum == request.TotalAmount;

        var invoice = new CardInvoice
        {
            HouseholdId = _currentUser.HouseholdId,
            CreditCardId = request.CreditCardId,
            ClosingDate = request.ClosingDate,
            DueDate = request.DueDate,
            TotalAmount = request.TotalAmount,
            OriginalFileName = request.OriginalFileName,
            CreatedBy = _currentUser.UserId
        };
        _db.CardInvoices.Add(invoice);

        var candidateTransactions = await _db.FinancialTransactions
            .Include(t => t.Installment)
            .Where(t => t.HouseholdId == _currentUser.HouseholdId && t.CreditCardId == request.CreditCardId && t.Status != TransactionStatus.Reconciled)
            .ToListAsync(ct);

        foreach (var itemRequest in request.Items)
        {
            var item = new CardInvoiceItem
            {
                CardInvoiceId = invoice.Id,
                Date = itemRequest.Date,
                Description = itemRequest.Description,
                InstallmentLabel = itemRequest.InstallmentLabel,
                Amount = itemRequest.Amount
            };
            _db.CardInvoiceItems.Add(item);

            var best = FindBestCandidate(item, candidateTransactions);

            var reconciliation = new Domain.Entities.Reconciliation
            {
                HouseholdId = _currentUser.HouseholdId,
                SourceType = ReconciliationSourceType.CardInvoice,
                CardInvoiceItemId = item.Id,
                CreatedBy = _currentUser.UserId
            };

            if (best is not null)
            {
                reconciliation.FinancialTransactionId = best.Value.transaction.Id;
                reconciliation.Score = best.Value.score.Total;
                reconciliation.AmountDifference = best.Value.score.AmountDifference;
                reconciliation.DateDifferenceDays = best.Value.score.DateDifferenceDays;
                reconciliation.DescriptionScore = best.Value.score.DescriptionScore;
                reconciliation.Status = ReconciliationScorer.StatusFor(best.Value.score.Total) switch
                {
                    "MATCHED" => ReconciliationStatus.Matched,
                    "POSSIBLE_MATCH" => ReconciliationStatus.PossibleMatch,
                    _ => ReconciliationStatus.Unmatched
                };
                reconciliation.Reason = $"Score {best.Value.score.Total} (valor/data/descrição/parcela)";
            }
            else
            {
                reconciliation.Status = ReconciliationStatus.Unmatched;
                reconciliation.Reason = "Nenhum lançamento candidato encontrado no período.";
            }

            _db.Reconciliations.Add(reconciliation);
        }

        await _db.SaveChangesAsync(ct);

        return new CardInvoiceResponse(invoice.Id, card.Id, card.Name, invoice.ClosingDate, invoice.DueDate,
            invoice.TotalAmount, totalValidated, request.Items.Count);
    }

    public async Task<IReadOnlyList<CardReconciliationResponse>> GetAllAsync(Guid? creditCardId, string? status, CancellationToken ct = default)
    {
        var query = _db.Reconciliations
            .AsNoTracking()
            .Include(r => r.CardInvoiceItem)
            .ThenInclude(i => i!.CardInvoice)
            .Where(r => r.HouseholdId == _currentUser.HouseholdId && r.SourceType == ReconciliationSourceType.CardInvoice);

        if (creditCardId is not null)
            query = query.Where(r => r.CardInvoiceItem!.CardInvoice!.CreditCardId == creditCardId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReconciliationStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        var reconciliations = await query.OrderByDescending(r => r.CardInvoiceItem!.Date).ToListAsync(ct);
        return reconciliations.Select(ToResponse).ToList();
    }

    public async Task<CardReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await FindOwnedAsync(reconciliationId, ct);

        if (reconciliation.FinancialTransactionId is null)
            throw new BusinessRuleException("conciliacao-sem-candidato", "Esta conciliação não possui um lançamento candidato para aceitar.");

        reconciliation.Status = ReconciliationStatus.Matched;

        var transaction = await _db.FinancialTransactions.FirstAsync(t => t.Id == reconciliation.FinancialTransactionId, ct);
        transaction.Status = TransactionStatus.Reconciled;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Accept", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), null, new { reconciliation.Status }, ct);

        return ToResponse(reconciliation);
    }

    public async Task<CardReconciliationResponse> LinkManuallyAsync(ManualCardReconciliationRequest request, CancellationToken ct = default)
    {
        var item = await _db.CardInvoiceItems.Include(i => i.CardInvoice)
            .FirstOrDefaultAsync(i => i.Id == request.CardInvoiceItemId, ct)
            ?? throw new NotFoundException(nameof(CardInvoiceItem), request.CardInvoiceItemId);
        if (item.CardInvoice!.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var transaction = await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == request.FinancialTransactionId, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.FinancialTransactionId);
        if (transaction.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var reconciliation = await _db.Reconciliations.FirstOrDefaultAsync(
            r => r.CardInvoiceItemId == request.CardInvoiceItemId && r.SourceType == ReconciliationSourceType.CardInvoice, ct);

        if (reconciliation is null)
        {
            reconciliation = new Domain.Entities.Reconciliation
            {
                HouseholdId = _currentUser.HouseholdId,
                SourceType = ReconciliationSourceType.CardInvoice,
                CardInvoiceItemId = item.Id,
                CreatedBy = _currentUser.UserId
            };
            _db.Reconciliations.Add(reconciliation);
        }

        reconciliation.FinancialTransactionId = transaction.Id;
        reconciliation.Status = ReconciliationStatus.ManuallyMatched;
        reconciliation.Reason = "Vinculado manualmente pelo usuário.";
        reconciliation.UpdatedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = _currentUser.UserId;

        transaction.Status = TransactionStatus.Reconciled;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ManualLink", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), null, request, ct);

        reconciliation.CardInvoiceItem = item;
        return ToResponse(reconciliation);
    }

    public async Task UndoAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await FindOwnedAsync(reconciliationId, ct);

        if (reconciliation.FinancialTransactionId is not null)
        {
            var transaction = await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == reconciliation.FinancialTransactionId, ct);
            if (transaction is not null)
            {
                transaction.Status = TransactionStatus.Confirmed;
                transaction.UpdatedAt = DateTime.UtcNow;
                transaction.UpdatedBy = _currentUser.UserId;
            }
        }

        var oldStatus = reconciliation.Status;
        reconciliation.Status = ReconciliationStatus.Unmatched;
        reconciliation.FinancialTransactionId = null;
        reconciliation.Reason = "Conciliação desfeita pelo usuário.";
        reconciliation.UpdatedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Undo", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), new { Status = oldStatus }, new { reconciliation.Status }, ct);
    }

    // ----------------- helpers -----------------

    private (FinancialTransaction transaction, ReconciliationScorer.ScoreResult score)? FindBestCandidate(
        CardInvoiceItem item, List<FinancialTransaction> candidates)
    {
        (FinancialTransaction transaction, ReconciliationScorer.ScoreResult score)? best = null;

        foreach (var transaction in candidates)
        {
            var dateDiff = Math.Abs(transaction.TransactionDate.DayNumber - item.Date.DayNumber);
            if (dateDiff > SearchWindowDays) continue;

            var sameInstallment = item.InstallmentLabel is not null && transaction.Installment is not null &&
                item.InstallmentLabel.Contains(transaction.Installment.Number.ToString());

            var score = ReconciliationScorer.Score(
                transaction.Amount, item.Amount,
                transaction.TransactionDate, item.Date,
                transaction.Description, item.Description,
                sameDocumentOrInstallment: sameInstallment);

            if (best is null || score.Total > best.Value.score.Total)
                best = (transaction, score);
        }

        return best is { score.Total: >= ReconciliationScorer.PossibleMatchThreshold } ? best : null;
    }

    private async Task<Domain.Entities.Reconciliation> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var reconciliation = await _db.Reconciliations.Include(r => r.CardInvoiceItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Domain.Entities.Reconciliation), id);
        if (reconciliation.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();
        return reconciliation;
    }

    private static CardReconciliationResponse ToResponse(Domain.Entities.Reconciliation r) => new(
        r.Id,
        r.CardInvoiceItemId ?? Guid.Empty,
        r.CardInvoiceItem?.Date ?? default,
        r.CardInvoiceItem?.Description ?? string.Empty,
        r.CardInvoiceItem?.Amount ?? 0,
        r.FinancialTransactionId,
        r.Status.ToString(),
        r.Score,
        r.AmountDifference,
        r.DateDifferenceDays,
        r.Reason
    );
}
