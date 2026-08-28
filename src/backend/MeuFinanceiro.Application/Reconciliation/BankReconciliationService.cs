using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>
/// docs/09-conciliacao-conta-corrente.md: valida saldo inicial + movimentos = saldo final
/// e propõe candidatos de conciliação por score (docs/08-conciliacao-cartoes.md, reaproveitado
/// para extrato de conta). O item de extrato usa valor com sinal (positivo = entrada, negativo = saída).
/// </summary>
public class BankReconciliationService : IBankReconciliationService
{
    private static readonly int[] SearchWindowDays = { 10 };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;

    public BankReconciliationService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService auditService)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<BankStatementResponse> ImportAsync(ImportBankStatementRequest request, CancellationToken ct = default)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
            ?? throw new NotFoundException(nameof(Account), request.AccountId);
        if (account.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var movementsSum = request.Items.Sum(i => i.Amount);
        var balanceValidated = request.OpeningBalance + movementsSum == request.ClosingBalance;

        var statement = new BankStatement
        {
            HouseholdId = _currentUser.HouseholdId,
            AccountId = request.AccountId,
            ReferenceStart = request.ReferenceStart,
            ReferenceEnd = request.ReferenceEnd,
            OpeningBalance = request.OpeningBalance,
            ClosingBalance = request.ClosingBalance,
            OriginalFileName = request.OriginalFileName,
            CreatedBy = _currentUser.UserId
        };
        _db.BankStatements.Add(statement);

        var candidateTransactions = await _db.FinancialTransactions
            .Where(t => t.HouseholdId == _currentUser.HouseholdId && t.AccountId == request.AccountId && t.Status != TransactionStatus.Reconciled)
            .ToListAsync(ct);

        foreach (var itemRequest in request.Items)
        {
            var item = new BankStatementItem
            {
                BankStatementId = statement.Id,
                Date = itemRequest.Date,
                Description = itemRequest.Description,
                Document = itemRequest.Document,
                ItemType = itemRequest.ItemType,
                Amount = itemRequest.Amount
            };
            _db.BankStatementItems.Add(item);

            var best = FindBestCandidate(item, candidateTransactions);

            var reconciliation = new Domain.Entities.Reconciliation
            {
                HouseholdId = _currentUser.HouseholdId,
                SourceType = ReconciliationSourceType.BankStatement,
                BankStatementItemId = item.Id,
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
                reconciliation.Reason = $"Score {best.Value.score.Total} (valor/data/descrição)";
            }
            else
            {
                reconciliation.Status = ReconciliationStatus.Unmatched;
                reconciliation.Reason = "Nenhum lançamento candidato encontrado no período.";
            }

            _db.Reconciliations.Add(reconciliation);
        }

        await _db.SaveChangesAsync(ct);

        return new BankStatementResponse(statement.Id, account.Id, account.Name, statement.ReferenceStart,
            statement.ReferenceEnd, statement.OpeningBalance, statement.ClosingBalance, balanceValidated, request.Items.Count);
    }

    public async Task<IReadOnlyList<BankReconciliationResponse>> GetAllAsync(Guid? accountId, string? status, CancellationToken ct = default)
    {
        var query = _db.Reconciliations
            .AsNoTracking()
            .Include(r => r.BankStatementItem)
            .ThenInclude(i => i!.BankStatement)
            .Where(r => r.HouseholdId == _currentUser.HouseholdId && r.SourceType == ReconciliationSourceType.BankStatement);

        if (accountId is not null)
            query = query.Where(r => r.BankStatementItem!.BankStatement!.AccountId == accountId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReconciliationStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        var reconciliations = await query.OrderByDescending(r => r.BankStatementItem!.Date).ToListAsync(ct);

        return reconciliations.Select(ToResponse).ToList();
    }

    public async Task<BankReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default)
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

    public async Task<BankReconciliationResponse> LinkManuallyAsync(ManualReconciliationRequest request, CancellationToken ct = default)
    {
        var item = await _db.BankStatementItems.Include(i => i.BankStatement)
            .FirstOrDefaultAsync(i => i.Id == request.BankStatementItemId, ct)
            ?? throw new NotFoundException(nameof(BankStatementItem), request.BankStatementItemId);
        if (item.BankStatement!.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var transaction = await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == request.FinancialTransactionId, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.FinancialTransactionId);
        if (transaction.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();

        var reconciliation = await _db.Reconciliations.FirstOrDefaultAsync(
            r => r.BankStatementItemId == request.BankStatementItemId && r.SourceType == ReconciliationSourceType.BankStatement, ct);

        if (reconciliation is null)
        {
            reconciliation = new Domain.Entities.Reconciliation
            {
                HouseholdId = _currentUser.HouseholdId,
                SourceType = ReconciliationSourceType.BankStatement,
                BankStatementItemId = item.Id,
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

        reconciliation.BankStatementItem = item;
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
        BankStatementItem item, List<FinancialTransaction> candidates)
    {
        (FinancialTransaction transaction, ReconciliationScorer.ScoreResult score)? best = null;

        foreach (var transaction in candidates)
        {
            var dateDiff = Math.Abs(transaction.TransactionDate.DayNumber - item.Date.DayNumber);
            if (dateDiff > SearchWindowDays[0]) continue;

            var signedTransactionAmount = transaction.Direction == TransactionDirection.Credit ? transaction.Amount : -transaction.Amount;

            var score = ReconciliationScorer.Score(
                signedTransactionAmount, item.Amount,
                transaction.TransactionDate, item.Date,
                transaction.Description, item.Description,
                sameDocumentOrInstallment: false);

            if (best is null || score.Total > best.Value.score.Total)
                best = (transaction, score);
        }

        return best is { score.Total: >= ReconciliationScorer.PossibleMatchThreshold } ? best : null;
    }

    private async Task<Domain.Entities.Reconciliation> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var reconciliation = await _db.Reconciliations.Include(r => r.BankStatementItem)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Domain.Entities.Reconciliation), id);
        if (reconciliation.HouseholdId != _currentUser.HouseholdId) throw new ForbiddenException();
        return reconciliation;
    }

    private static BankReconciliationResponse ToResponse(Domain.Entities.Reconciliation r) => new(
        r.Id,
        r.BankStatementItemId ?? Guid.Empty,
        r.BankStatementItem?.Date ?? default,
        r.BankStatementItem?.Description ?? string.Empty,
        r.BankStatementItem?.Amount ?? 0,
        r.FinancialTransactionId,
        r.Status.ToString(),
        r.Score,
        r.AmountDifference,
        r.DateDifferenceDays,
        r.Reason
    );
}
