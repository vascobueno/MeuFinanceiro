using MeuFinanceiro.Application.Reconciliation.Import;
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
        if (account.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var movementsSum = request.Items.Sum(i => i.Amount);
        var balanceValidated = request.OpeningBalance + movementsSum == request.ClosingBalance;

        // Antes o extrato que não fecha entrava calado, e o aviso só existia na resposta.
        if (!balanceValidated && !request.ConfirmUnbalanced)
        {
            var computed = request.OpeningBalance + movementsSum;
            throw new BusinessRuleException("extrato-nao-fecha",
                $"A soma dos movimentos não bate com o saldo final: saldo inicial {ImportValueParser.FormatBrl(request.OpeningBalance)} " +
                $"+ movimentos {ImportValueParser.FormatBrl(movementsSum)} = {ImportValueParser.FormatBrl(computed)}, mas o saldo final " +
                $"informado é {ImportValueParser.FormatBrl(request.ClosingBalance)} (diferença de " +
                $"{ImportValueParser.FormatBrl(request.ClosingBalance - computed)}). Confira os valores ou importe mesmo assim.");
        }

        // O mesmo extrato importado duas vezes virava dois, com itens de conciliação em dobro.
        var contentHash = ImportContentHash.ForStatement(request);
        var existing = await _db.BankStatements.AsNoTracking()
            .Where(s => s.GrupoId == _currentUser.GrupoId && s.ContentHash == contentHash)
            .Select(s => new { s.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            throw new BusinessRuleException("extrato-duplicado",
                $"Este extrato já foi importado em {existing.CreatedAt.ToLocalTime():dd/MM/yyyy}. " +
                "Se a importação anterior estava errada, exclua-a na tela Extratos e importe de novo.");
        }

        // OFX que se sobrepõe a um já importado: os movimentos com FITID conhecido nesta conta ficam
        // de fora. O saldo foi conferido acima com o arquivo inteiro, que é o que o banco fechou.
        var newItems = await WithoutKnownExternalIdsAsync(request, ct);
        var skipped = request.Items.Count - newItems.Count;
        if (request.Items.Count > 0 && newItems.Count == 0)
            throw new BusinessRuleException("extrato-sem-movimentos-novos",
                $"Os {request.Items.Count} movimentos deste arquivo já foram importados nesta conta. Nada a importar.");

        var statement = new BankStatement
        {
            GrupoId = _currentUser.GrupoId,
            AccountId = request.AccountId,
            ReferenceStart = request.ReferenceStart,
            ReferenceEnd = request.ReferenceEnd,
            OpeningBalance = request.OpeningBalance,
            ClosingBalance = request.ClosingBalance,
            OriginalFileName = request.OriginalFileName,
            ContentHash = contentHash,
            BalanceValidated = balanceValidated,
            CreatedBy = _currentUser.UserId
        };
        _db.BankStatements.Add(statement);

        // A janela de busca já era aplicada, mas só dentro do laço de scoring (FindBestCandidate),
        // depois de trazer TODOS os lançamentos não conciliados da conta para a memória. Subindo o
        // recorte para o WHERE, o produto cartesiano do matching passa a ser sobre o período do
        // extrato em vez de sobre o histórico inteiro.
        var candidateTransactions = new List<FinancialTransaction>();
        if (newItems.Count > 0)
        {
            var windowStart = newItems.Min(i => i.EffectiveDate is { } e && e < i.Date ? e : i.Date).AddDays(-SearchWindowDays[0]);
            var windowEnd = newItems.Max(i => i.Date).AddDays(SearchWindowDays[0]);

            candidateTransactions = await _db.FinancialTransactions
                .Where(t => t.GrupoId == _currentUser.GrupoId && t.AccountId == request.AccountId && t.Status != TransactionStatus.Reconciled)
                .Where(t => t.TransactionDate >= windowStart && t.TransactionDate <= windowEnd)
                .ToListAsync(ct);
        }

        foreach (var itemRequest in newItems)
        {
            var item = new BankStatementItem
            {
                BankStatementId = statement.Id,
                Date = itemRequest.Date,
                Description = itemRequest.Description,
                Document = itemRequest.Document,
                // Tipo vazio ou desconhecido (cliente antigo, colagem estruturada com erro) é classificado aqui.
                ItemType = StatementItemClassifier.Resolve(itemRequest.ItemType, itemRequest.Description, itemRequest.Amount),
                EffectiveDate = itemRequest.EffectiveDate,
                ExternalId = string.IsNullOrWhiteSpace(itemRequest.ExternalId) ? null : itemRequest.ExternalId.Trim(),
                Amount = itemRequest.Amount
            };
            _db.BankStatementItems.Add(item);

            var best = FindBestCandidate(item, candidateTransactions);

            var reconciliation = new Domain.Entities.Reconciliation
            {
                GrupoId = _currentUser.GrupoId,
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
            statement.ReferenceEnd, statement.OpeningBalance, statement.ClosingBalance, balanceValidated, newItems.Count, skipped);
    }

    /// <summary>
    /// Lê o OFX e reconhece a conta pelos dados bancários. Não grava nada: a tela confere e manda
    /// os itens para <see cref="ImportAsync"/>, o mesmo caminho do extrato colado.
    /// </summary>
    public async Task<ParsedOfxResponse> ParseOfxAsync(byte[] content, CancellationToken ct = default)
    {
        var ofx = OfxStatementReader.Read(OfxStatementReader.Decode(content));

        var candidates = await _db.Accounts.AsNoTracking()
            .Where(a => a.GrupoId == _currentUser.GrupoId && a.AccountNumber != null)
            .ToListAsync(ct);
        var matches = candidates.Where(a => OfxAccountMatcher.Matches(a, ofx.BankId, ofx.BranchId, ofx.AccountId)).ToList();
        var account = matches.Count == 1 ? matches[0] : null;

        var sum = ofx.Items.Sum(i => i.Amount);
        var start = ofx.Start ?? (ofx.Items.Count > 0 ? ofx.Items.Min(i => i.Date) : null);
        var end = ofx.End ?? (ofx.Items.Count > 0 ? ofx.Items.Max(i => i.Date) : null);

        var alreadyImported = 0;
        if (account is not null)
        {
            var probe = new ImportBankStatementRequest(account.Id, default, default, 0, 0, null, ofx.Items);
            alreadyImported = ofx.Items.Count - (await WithoutKnownExternalIdsAsync(probe, ct)).Count;
        }

        return new ParsedOfxResponse(
            account?.Id, account?.Name,
            OfxAccountMatcher.Describe(ofx.BankId, ofx.BranchId, ofx.AccountId),
            matches.Count, start, end,
            ofx.LedgerBalance is { } ledger ? ledger - sum : null, ofx.LedgerBalance,
            ofx.Items, ofx.Errors, sum, alreadyImported);
    }

    private async Task<List<BankStatementItemRequest>> WithoutKnownExternalIdsAsync(ImportBankStatementRequest request, CancellationToken ct)
    {
        var ids = request.Items.Where(i => !string.IsNullOrWhiteSpace(i.ExternalId)).Select(i => i.ExternalId!.Trim()).Distinct().ToList();
        if (ids.Count == 0) return request.Items.ToList();

        var known = (await _db.BankStatementItems.AsNoTracking()
                .Where(i => i.ExternalId != null && ids.Contains(i.ExternalId)
                            && i.BankStatement!.AccountId == request.AccountId
                            && i.BankStatement.GrupoId == _currentUser.GrupoId)
                .Select(i => i.ExternalId!)
                .ToListAsync(ct))
            .ToHashSet();

        return request.Items.Where(i => string.IsNullOrWhiteSpace(i.ExternalId) || !known.Contains(i.ExternalId.Trim())).ToList();
    }

    public async Task<IReadOnlyList<BankReconciliationResponse>> GetAllAsync(Guid? accountId, string? status, CancellationToken ct = default)
    {
        var query = _db.Reconciliations
            .AsNoTracking()
            .Include(r => r.BankStatementItem)
            .ThenInclude(i => i!.BankStatement)
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.SourceType == ReconciliationSourceType.BankStatement);

        if (accountId is not null)
            query = query.Where(r => r.BankStatementItem!.BankStatement!.AccountId == accountId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReconciliationStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        var reconciliations = await query.OrderByDescending(r => r.BankStatementItem!.Date).ToListAsync(ct);

        return reconciliations.Select(ToResponse).ToList();
    }

    public ParsedBankStatementResponse Parse(ParseBankStatementRequest request) =>
        StatementTextReader.Read(request.Text, request.ReferenceStart, request.ReferenceEnd);

    /// <summary>
    /// Exclui um extrato importado por engano, com os itens e as sugestões de conciliação.
    /// Bloqueado se algum item já levou um lançamento a "conciliado": desfazer aquilo em silêncio
    /// apagaria uma conferência que o usuário fez.
    /// </summary>
    public async Task DeleteStatementAsync(Guid statementId, CancellationToken ct = default)
    {
        var statement = await _db.BankStatements.FirstOrDefaultAsync(s => s.Id == statementId, ct)
            ?? throw new NotFoundException(nameof(BankStatement), statementId);
        if (statement.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var reconciliations = await _db.Reconciliations
            .Include(r => r.FinancialTransaction)
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.BankStatementItem!.BankStatementId == statementId)
            .ToListAsync(ct);

        if (reconciliations.Any(r => r.FinancialTransaction?.Status == TransactionStatus.Reconciled))
        {
            throw new BusinessRuleException("extrato-com-conciliacao-aceita",
                "Este extrato tem conciliações aceitas. Desfaça-as na tela de conciliação antes de excluir.");
        }

        // Os itens são carregados e removidos explicitamente, em vez de confiar no cascade do
        // banco: sem eles rastreados, o EF não sabe que as conciliações dependem dos itens e
        // pode apagar o extrato primeiro — e aí o cascade dos itens esbarra na chave estrangeira
        // das conciliações (erro 23503 no PostgreSQL; o banco em memória dos testes não acusa).
        var items = await _db.BankStatementItems.Where(i => i.BankStatementId == statementId).ToListAsync(ct);

        _db.Reconciliations.RemoveRange(reconciliations);
        _db.BankStatementItems.RemoveRange(items);
        _db.BankStatements.Remove(statement);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Delete", nameof(BankStatement), statementId.ToString(), null, null, ct);
    }

    public async Task<BankReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await FindOwnedAsync(reconciliationId, ct);

        if (reconciliation.FinancialTransactionId is null)
            throw new BusinessRuleException("conciliacao-sem-candidato", "Esta conciliação não possui um lançamento candidato para aceitar.");

        reconciliation.Status = ReconciliationStatus.Matched;
        reconciliation.AcceptedAt = DateTime.UtcNow;

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
        if (item.BankStatement!.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var transaction = await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == request.FinancialTransactionId, ct)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.FinancialTransactionId);
        if (transaction.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var reconciliation = await _db.Reconciliations.FirstOrDefaultAsync(
            r => r.BankStatementItemId == request.BankStatementItemId && r.SourceType == ReconciliationSourceType.BankStatement, ct);

        if (reconciliation is null)
        {
            reconciliation = new Domain.Entities.Reconciliation
            {
                GrupoId = _currentUser.GrupoId,
                SourceType = ReconciliationSourceType.BankStatement,
                BankStatementItemId = item.Id,
                CreatedBy = _currentUser.UserId
            };
            _db.Reconciliations.Add(reconciliation);
        }

        reconciliation.FinancialTransactionId = transaction.Id;
        reconciliation.Status = ReconciliationStatus.ManuallyMatched;
        reconciliation.AcceptedAt = DateTime.UtcNow;
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
        reconciliation.AcceptedAt = null;
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

        // A compra no débito é registrada na data da compra, não na do lançamento no extrato.
        var itemDate = item.EffectiveDate ?? item.Date;

        foreach (var transaction in candidates)
        {
            var dateDiff = Math.Abs(transaction.TransactionDate.DayNumber - itemDate.DayNumber);
            if (dateDiff > SearchWindowDays[0]) continue;

            var signedTransactionAmount = transaction.Direction == TransactionDirection.Credit ? transaction.Amount : -transaction.Amount;

            var score = ReconciliationScorer.Score(
                signedTransactionAmount, item.Amount,
                transaction.TransactionDate, itemDate,
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
        if (reconciliation.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
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
        r.Reason,
        r.BankStatementItem?.ItemType ?? "OTHER_EXPENSE",
        r.BankStatementItem?.EffectiveDate
    );
}
