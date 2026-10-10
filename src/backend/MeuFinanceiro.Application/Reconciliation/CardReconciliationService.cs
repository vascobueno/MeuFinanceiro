using MeuFinanceiro.Application.Reconciliation.Import;
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

    private readonly IPdfTextExtractor _pdf;

    public CardReconciliationService(IApplicationDbContext db, ICurrentUserService currentUser, IAuditService auditService, IPdfTextExtractor pdf)
    {
        _db = db;
        _currentUser = currentUser;
        _auditService = auditService;
        _pdf = pdf;
    }

    public async Task<CardInvoiceResponse> ImportAsync(ImportCardInvoiceRequest request, CancellationToken ct = default)
    {
        var card = await _db.CreditCards.FirstOrDefaultAsync(c => c.Id == request.CreditCardId, ct)
            ?? throw new NotFoundException(nameof(CreditCard), request.CreditCardId);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var itemsSum = request.Items.Sum(i => i.Amount);
        // A fatura fecha como saldo anterior + itens (o pagamento entra negativo) = total.
        var totalValidated = request.PreviousBalance + itemsSum == request.TotalAmount;

        if (!totalValidated && !request.ConfirmUnbalanced)
        {
            var computed = request.PreviousBalance + itemsSum;
            throw new BusinessRuleException("fatura-nao-fecha",
                $"A soma dos itens não bate com o total da fatura: saldo anterior {ImportValueParser.FormatBrl(request.PreviousBalance)} " +
                $"+ itens {ImportValueParser.FormatBrl(itemsSum)} = {ImportValueParser.FormatBrl(computed)}, mas o total informado é " +
                $"{ImportValueParser.FormatBrl(request.TotalAmount)} (diferença de {ImportValueParser.FormatBrl(request.TotalAmount - computed)}). " +
                "Confira os valores ou importe mesmo assim.");
        }

        var contentHash = ImportContentHash.ForInvoice(request);
        var existing = await _db.CardInvoices.AsNoTracking()
            .Where(i => i.GrupoId == _currentUser.GrupoId && i.ContentHash == contentHash)
            .Select(i => new { i.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            throw new BusinessRuleException("fatura-duplicada",
                $"Esta fatura já foi importada em {existing.CreatedAt.ToLocalTime():dd/MM/yyyy}. " +
                "Se a importação anterior estava errada, exclua-a na tela Faturas e importe de novo.");
        }

        var invoice = new CardInvoice
        {
            GrupoId = _currentUser.GrupoId,
            CreditCardId = request.CreditCardId,
            ClosingDate = request.ClosingDate,
            DueDate = request.DueDate,
            TotalAmount = request.TotalAmount,
            PreviousBalance = request.PreviousBalance,
            OriginalFileName = request.OriginalFileName,
            ContentHash = contentHash,
            BalanceValidated = totalValidated,
            CreatedBy = _currentUser.UserId
        };
        _db.CardInvoices.Add(invoice);

        // Mesma correção do extrato bancário: a janela de busca (15 dias) era aplicada só dentro
        // do laço de scoring, depois de carregar todos os lançamentos não conciliados do cartão.
        var candidateTransactions = new List<FinancialTransaction>();
        if (request.Items.Count > 0)
        {
            var windowStart = request.Items.Min(i => i.Date).AddDays(-SearchWindowDays);
            var windowEnd = request.Items.Max(i => i.Date).AddDays(SearchWindowDays);

            candidateTransactions = await _db.FinancialTransactions
                .Include(t => t.Installment).ThenInclude(i => i!.InstallmentPlan)
                .Where(t => t.GrupoId == _currentUser.GrupoId && t.CreditCardId == request.CreditCardId && t.Status != TransactionStatus.Reconciled)
                .Where(t => t.TransactionDate >= windowStart && t.TransactionDate <= windowEnd)
                .ToListAsync(ct);
        }

        var installmentCandidates = await LoadInstallmentCandidatesAsync(request, ct);
        var paymentCandidates = await LoadPaymentCandidatesAsync(request, ct);
        // Um lançamento ou pagamento serve a um item só. Sem isso, duas parcelas iguais de planos
        // diferentes (ou duas compras de mesmo valor) apontavam para o mesmo lançamento.
        var usedTransactions = new HashSet<Guid>();
        var usedPayments = new HashSet<Guid>();

        // Cartões do grupo por final, para ligar cada item ao cartão da seção em que veio. O
        // substituído também conta: a fatura ainda traz compras dele. Com dois do mesmo final
        // (renovação), vale o ativo.
        var cardsByFinal = (await _db.IssuedCards.AsNoTracking()
                .Where(c => c.GrupoId == _currentUser.GrupoId && c.CreditCardId == request.CreditCardId && c.Status != IssuedCardStatus.Cancelado)
                .Select(c => new { c.Id, c.LastFour, c.Status })
                .ToListAsync(ct))
            .GroupBy(c => c.LastFour)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Status).First().Id);
        var created = new List<CardInvoiceItem>();

        foreach (var itemRequest in request.Items)
        {
            var item = new CardInvoiceItem
            {
                CardInvoiceId = invoice.Id,
                Date = itemRequest.Date,
                Description = itemRequest.Description,
                InstallmentLabel = itemRequest.InstallmentLabel,
                Amount = itemRequest.Amount,
                Kind = itemRequest.Kind ?? InvoiceTextReader.Classify(itemRequest.Description, itemRequest.Amount, itemRequest.InstallmentLabel),
                InstallmentNumber = itemRequest.InstallmentNumber ?? InstallmentLabel.TryParse(itemRequest.InstallmentLabel)?.Number,
                InstallmentTotal = itemRequest.InstallmentTotal ?? InstallmentLabel.TryParse(itemRequest.InstallmentLabel)?.Total,
                CardLastFour = itemRequest.CardLastFour,
                IssuedCardId = itemRequest.CardLastFour is { } final && cardsByFinal.TryGetValue(final, out var issuedId) ? issuedId : null,
                ForeignAmount = itemRequest.ForeignAmount,
                ForeignCurrency = itemRequest.ForeignAmount is null ? null : itemRequest.ForeignCurrency?.ToUpperInvariant(),
                ExchangeRate = itemRequest.ForeignAmount is null ? null : itemRequest.ExchangeRate,
                ExchangeRateEstimated = itemRequest.ForeignAmount is not null && itemRequest.ExchangeRateEstimated,
                OriginalAmount = itemRequest.OriginalAmount,
                OriginalCurrency = itemRequest.OriginalAmount is null ? null : itemRequest.OriginalCurrency?.ToUpperInvariant(),
            };
            _db.CardInvoiceItems.Add(item);
            created.Add(item);

            var reconciliation = new Domain.Entities.Reconciliation
            {
                GrupoId = _currentUser.GrupoId,
                SourceType = ReconciliationSourceType.CardInvoice,
                CardInvoiceItemId = item.Id,
                CreatedBy = _currentUser.UserId
            };

            // Valor zero (anuidade isenta, "R$ 0,00 parcela 08/12"): nada a conciliar nem a lançar.
            if (item.Amount == 0)
            {
                reconciliation.Status = ReconciliationStatus.Ignored;
                reconciliation.Reason = "Valor zero — nada a conciliar.";
                _db.Reconciliations.Add(reconciliation);
                continue;
            }

            var payment = IsInvoicePayment(item)
                ? FindBestPayment(item, invoice.ClosingDate, paymentCandidates, usedPayments)
                : null;
            var installment = payment is null
                ? FindBestInstallment(item, installmentCandidates, usedTransactions)
                : null;
            var best = payment is null && installment is null
                ? FindBestCandidate(item, candidateTransactions, usedTransactions)
                : null;

            if (payment is not null)
            {
                reconciliation.CardPaymentId = payment.Value.payment.Id;
                usedPayments.Add(payment.Value.payment.Id);
                ApplyScore(reconciliation, payment.Value.score,
                    $"Pagamento de {payment.Value.payment.PaymentDate:dd/MM/yyyy} da fatura {payment.Value.payment.CycleReference:MM/yyyy}");
            }
            else if (installment is not null)
            {
                reconciliation.FinancialTransactionId = installment.Value.transaction.Id;
                usedTransactions.Add(installment.Value.transaction.Id);
                var plan = installment.Value.transaction.Installment!.InstallmentPlan!;
                ApplyScore(reconciliation, installment.Value.score,
                    $"Parcela {installment.Value.transaction.Installment.Number}/{plan.InstallmentCount} de \"{plan.Description}\" (1ª parcela em {installment.Value.firstDate:dd/MM/yyyy})");
            }
            else if (best is not null)
            {
                reconciliation.FinancialTransactionId = best.Value.transaction.Id;
                usedTransactions.Add(best.Value.transaction.Id);
                ApplyScore(reconciliation, best.Value.score,
                    $"\"{best.Value.transaction.Description}\" de {best.Value.transaction.TransactionDate:dd/MM/yyyy}");
            }
            else
            {
                reconciliation.Status = ReconciliationStatus.Unmatched;
                reconciliation.Reason = IsInvoicePayment(item)
                    ? "Nenhum pagamento de fatura registrado com este valor e data."
                    : "Nenhum lançamento candidato encontrado no período.";
            }

            _db.Reconciliations.Add(reconciliation);
        }

        // O IOF aponta a compra no exterior pela posição na lista enviada.
        for (var i = 0; i < request.Items.Count; i++)
        {
            if (request.Items[i].RelatedIndex is { } related && related >= 0 && related < created.Count && related != i)
                created[i].RelatedItemId = created[related].Id;
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
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.SourceType == ReconciliationSourceType.CardInvoice);

        if (creditCardId is not null)
            query = query.Where(r => r.CardInvoiceItem!.CardInvoice!.CreditCardId == creditCardId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReconciliationStatus>(status, true, out var parsedStatus))
            query = query.Where(r => r.Status == parsedStatus);

        var reconciliations = await query.OrderByDescending(r => r.CardInvoiceItem!.Date).ToListAsync(ct);
        return reconciliations.Select(ToResponse).ToList();
    }

    /// <summary>
    /// Lê o texto e reconhece o grupo de cartões pelos finais dos cabeçalhos: só quando todos os
    /// finais conhecidos são do mesmo grupo. Finais que não estão cadastrados voltam sem cartão —
    /// a tela oferece "adicionar ao grupo".
    /// </summary>
    public async Task<ParsedCardInvoiceResponse> ParseAsync(ParseCardInvoiceRequest request, CancellationToken ct = default) =>
        await DetectGroupAsync(InvoiceTextReader.Read(request.Text, request.ClosingDate), ct);

    /// <summary>
    /// A fatura em PDF: o texto sai do PDF (com a senha, que só abre o arquivo — não é guardada nem
    /// registrada) e segue o mesmo caminho do texto colado. Nada é gravado: nem o PDF, nem o texto.
    /// Sem o fechamento informado, ele vem do próprio resumo da fatura — lido uma vez com a data de
    /// hoje como referência do ano e de novo com o fechamento encontrado.
    /// </summary>
    public async Task<ParsedCardInvoiceResponse> ParsePdfAsync(byte[] pdf, string? password, DateOnly? closingDate, CancellationToken ct = default)
    {
        var text = _pdf.ExtractText(pdf, password);
        var reference = closingDate ?? InvoiceTextReader.Read(text, DateOnly.FromDateTime(DateTime.Today)).ClosingDateFromText
                        ?? DateOnly.FromDateTime(DateTime.Today);
        var parsed = InvoiceTextReader.Read(text, reference);
        if (parsed.Items.Count == 0)
            throw new BusinessRuleException("pdf-sem-itens",
                "Não foi encontrado nenhum item de fatura neste PDF. Ele é a fatura do cartão (Santander ou Pan)?");
        return await DetectGroupAsync(parsed, ct);
    }

    private async Task<ParsedCardInvoiceResponse> DetectGroupAsync(ParsedCardInvoiceResponse parsed, CancellationToken ct)
    {
        var headers = parsed.Cards ?? [];
        if (headers.Count == 0) return parsed;

        var finals = headers.Select(h => h.LastFour).ToList();
        var known = await _db.IssuedCards.AsNoTracking()
            .Where(c => c.GrupoId == _currentUser.GrupoId && c.CreditCardId != null && c.Status != IssuedCardStatus.Cancelado
                        && finals.Contains(c.LastFour))
            .Select(c => new { c.Id, c.LastFour, c.Status, c.CreditCardId, GroupName = c.CreditCard!.Name })
            .ToListAsync(ct);

        var cards = headers.Select(h =>
        {
            var match = known.Where(k => k.LastFour == h.LastFour).OrderBy(k => k.Status).FirstOrDefault();
            return match is null ? h : h with { IssuedCardId = match.Id, CreditCardId = match.CreditCardId, CreditCardName = match.GroupName };
        }).ToList();

        var groups = cards.Where(c => c.CreditCardId is not null).Select(c => (c.CreditCardId, c.CreditCardName)).Distinct().ToList();
        return parsed with
        {
            Cards = cards,
            DetectedCreditCardId = groups.Count == 1 ? groups[0].CreditCardId : null,
            DetectedCreditCardName = groups.Count == 1 ? groups[0].CreditCardName : null,
        };
    }

    /// <summary>
    /// Exclui uma fatura importada por engano, com itens e sugestões. Bloqueado se algum item já
    /// levou um lançamento a "conciliado" — ver BankReconciliationService.DeleteStatementAsync.
    /// </summary>
    public async Task DeleteInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.CardInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new NotFoundException(nameof(CardInvoice), invoiceId);
        if (invoice.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var reconciliations = await _db.Reconciliations
            .Include(r => r.FinancialTransaction)
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.CardInvoiceItem!.CardInvoiceId == invoiceId)
            .ToListAsync(ct);

        if (reconciliations.Any(r => r.AcceptedAt is not null || r.FinancialTransaction?.Status == TransactionStatus.Reconciled))
        {
            throw new BusinessRuleException("fatura-com-conciliacao-aceita",
                "Esta fatura tem conciliações aceitas. Desfaça-as na tela de conciliação antes de excluir.");
        }

        // Itens carregados explicitamente — ver o comentário em BankReconciliationService.
        var items = await _db.CardInvoiceItems.Where(i => i.CardInvoiceId == invoiceId).ToListAsync(ct);

        _db.Reconciliations.RemoveRange(reconciliations);
        _db.CardInvoiceItems.RemoveRange(items);
        _db.CardInvoices.Remove(invoice);
        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Delete", nameof(CardInvoice), invoiceId.ToString(), null, null, ct);
    }

    public async Task<CardReconciliationResponse> AcceptAsync(Guid reconciliationId, CancellationToken ct = default)
    {
        var reconciliation = await FindOwnedAsync(reconciliationId, ct);

        if (reconciliation.FinancialTransactionId is null && reconciliation.CardPaymentId is null)
            throw new BusinessRuleException("conciliacao-sem-candidato", "Esta conciliação não possui um lançamento candidato para aceitar.");

        reconciliation.Status = ReconciliationStatus.Matched;
        reconciliation.AcceptedAt = DateTime.UtcNow;

        // O pagamento não muda de status: o lançamento dele está na conta e é conciliado lá,
        // com o extrato. Aqui o aceite fica só no AcceptedAt.
        if (reconciliation.FinancialTransactionId is not null)
        {
            var transaction = await _db.FinancialTransactions.FirstAsync(t => t.Id == reconciliation.FinancialTransactionId, ct);
            if (reconciliation.CardInvoiceItem is { } invoiceItem) Transactions.ForeignPurchase.CopyFromInvoiceItem(transaction, invoiceItem);
            transaction.Status = TransactionStatus.Reconciled;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Accept", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), null, new { reconciliation.Status }, ct);

        return ToResponse(reconciliation);
    }

    public async Task<CardReconciliationResponse> LinkManuallyAsync(ManualCardReconciliationRequest request, CancellationToken ct = default)
    {
        var item = await _db.CardInvoiceItems.Include(i => i.CardInvoice)
            .FirstOrDefaultAsync(i => i.Id == request.CardInvoiceItemId, ct)
            ?? throw new NotFoundException(nameof(CardInvoiceItem), request.CardInvoiceItemId);
        if (item.CardInvoice!.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        if ((request.FinancialTransactionId is null) == (request.CardPaymentId is null))
            throw new BusinessRuleException("conciliacao-alvo-invalido", "Informe um lançamento ou um pagamento de fatura — um dos dois.");

        FinancialTransaction? transaction = null;
        CardPayment? payment = null;
        if (request.FinancialTransactionId is not null)
        {
            transaction = await _db.FinancialTransactions.FirstOrDefaultAsync(t => t.Id == request.FinancialTransactionId, ct)
                ?? throw new NotFoundException(nameof(FinancialTransaction), request.FinancialTransactionId);
            if (transaction.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        }
        else
        {
            payment = await _db.CardPayments.FirstOrDefaultAsync(p => p.Id == request.CardPaymentId, ct)
                ?? throw new NotFoundException(nameof(CardPayment), request.CardPaymentId!);
            if (payment.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
            if (payment.CreditCardId != item.CardInvoice.CreditCardId)
                throw new BusinessRuleException("pagamento-de-outro-cartao", "Este pagamento é de outro cartão.");
        }

        var reconciliation = await _db.Reconciliations.FirstOrDefaultAsync(
            r => r.CardInvoiceItemId == request.CardInvoiceItemId && r.SourceType == ReconciliationSourceType.CardInvoice, ct);

        if (reconciliation is null)
        {
            reconciliation = new Domain.Entities.Reconciliation
            {
                GrupoId = _currentUser.GrupoId,
                SourceType = ReconciliationSourceType.CardInvoice,
                CardInvoiceItemId = item.Id,
                CreatedBy = _currentUser.UserId
            };
            _db.Reconciliations.Add(reconciliation);
        }

        reconciliation.FinancialTransactionId = transaction?.Id;
        reconciliation.CardPaymentId = payment?.Id;
        reconciliation.Status = ReconciliationStatus.ManuallyMatched;
        reconciliation.AcceptedAt = DateTime.UtcNow;
        reconciliation.Reason = "Vinculado manualmente pelo usuário.";
        reconciliation.UpdatedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = _currentUser.UserId;

        if (transaction is not null)
        {
            Transactions.ForeignPurchase.CopyFromInvoiceItem(transaction, item);
            transaction.Status = TransactionStatus.Reconciled;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.UpdatedBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("ManualLink", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), null, request, ct);

        reconciliation.CardInvoiceItem = item;
        return ToResponse(reconciliation);
    }

    /// <summary>
    /// "Lançar": cria no cartão o lançamento de um item da fatura que não tinha par — anuidade,
    /// seguro, IOF, a compra esquecida — e já o deixa conciliado com o item. Pagamento não entra:
    /// ele se registra em "Pagar fatura", porque mexe na conta.
    /// </summary>
    public async Task<CardReconciliationResponse> CreateTransactionFromItemAsync(Guid reconciliationId, CreateFromInvoiceItemRequest request, CancellationToken ct = default)
    {
        var reconciliation = await FindOwnedAsync(reconciliationId, ct);
        var item = await _db.CardInvoiceItems.Include(i => i.CardInvoice)
            .FirstAsync(i => i.Id == reconciliation.CardInvoiceItemId, ct);

        if (reconciliation.AcceptedAt is not null)
            throw new BusinessRuleException("conciliacao-ja-aceita", "Este item já está conciliado. Desfaça a conciliação para lançá-lo de novo.");
        if (item.Kind == CardInvoiceItemKind.Payment)
            throw new BusinessRuleException("item-de-pagamento",
                "Pagamento de fatura não é lançamento do cartão: registre-o em \"Pagar fatura\", nos lançamentos do cartão.");
        if (item.Amount == 0)
            throw new BusinessRuleException("item-sem-valor", "Item de valor zero não tem o que lançar.");

        if (request.CategoryId is { } categoryId)
        {
            var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
                ?? throw new NotFoundException(nameof(Category), categoryId);
            if (category.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        }

        // Estorno e crédito reduzem a fatura: despesa com direção crédito, como o lançamento
        // manual de um estorno no cartão.
        var transaction = new FinancialTransaction
        {
            GrupoId = _currentUser.GrupoId,
            TransactionDate = item.Date,
            Description = item.InstallmentLabel is null ? item.Description : $"{item.Description} ({item.InstallmentLabel})",
            CreditCardId = item.CardInvoice!.CreditCardId,
            CategoryId = request.CategoryId,
            Type = TransactionType.Expense,
            Direction = item.Amount < 0 ? TransactionDirection.Credit : TransactionDirection.Debit,
            Amount = Math.Abs(item.Amount),
            Status = TransactionStatus.Reconciled,
            Notes = "Lançado a partir da fatura importada.",
            IssuedCardId = item.IssuedCardId,
            CreatedBy = _currentUser.UserId,
        };
        Transactions.ForeignPurchase.CopyFromInvoiceItem(transaction, item);
        _db.FinancialTransactions.Add(transaction);

        // Se havia sugestão de outro lançamento, ela é trocada pelo novo.
        reconciliation.FinancialTransactionId = transaction.Id;
        reconciliation.CardPaymentId = null;
        reconciliation.Status = ReconciliationStatus.ManuallyMatched;
        reconciliation.AcceptedAt = DateTime.UtcNow;
        reconciliation.Reason = "Lançado a partir do item da fatura.";
        reconciliation.UpdatedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("CreateFromInvoiceItem", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), null,
            new { transaction.Id, transaction.Amount }, ct);

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
        reconciliation.CardPaymentId = null;
        reconciliation.AcceptedAt = null;
        reconciliation.Reason = "Conciliação desfeita pelo usuário.";
        reconciliation.UpdatedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync("Undo", nameof(Domain.Entities.Reconciliation), reconciliation.Id.ToString(), new { Status = oldStatus }, new { reconciliation.Status }, ct);
    }

    // ----------------- helpers -----------------

    /// <summary>
    /// Parcelas com o mesmo número e total das que a fatura traz, sem olhar a data do lançamento:
    /// a fatura lista a parcela com a data da COMPRA ("19/05 LOJA 04/06" numa fatura de setembro),
    /// enquanto o lançamento da 4ª parcela está datado em agosto. A janela de 15 dias em torno da
    /// data do item descartava todas elas.
    /// </summary>
    private async Task<List<InstallmentCandidate>> LoadInstallmentCandidatesAsync(ImportCardInvoiceRequest request, CancellationToken ct)
    {
        var labels = request.Items.Select(i => InstallmentLabel.TryParse(i.InstallmentLabel))
            .Where(l => l is not null).Select(l => l!.Value).ToList();
        if (labels.Count == 0) return [];

        var numbers = labels.Select(l => l.Number).Distinct().ToList();
        var totals = labels.Select(l => l.Total).Distinct().ToList();

        var transactions = await _db.FinancialTransactions
            .Include(t => t.Installment).ThenInclude(i => i!.InstallmentPlan)
            .Where(t => t.GrupoId == _currentUser.GrupoId && t.CreditCardId == request.CreditCardId && t.Status != TransactionStatus.Reconciled)
            .Where(t => t.InstallmentId != null
                        && numbers.Contains(t.Installment!.Number)
                        && totals.Contains(t.Installment.InstallmentPlan!.InstallmentCount))
            .ToListAsync(ct);
        if (transactions.Count == 0) return [];

        // A data da 1ª parcela é a que corresponde à data da compra impressa na fatura. Lida do
        // banco, e não recalculada com AddMonths(-(n-1)), que erra quando o dia é 29–31.
        var planIds = transactions.Select(t => t.Installment!.InstallmentPlanId).Distinct().ToList();
        var firstDates = await _db.Installments.AsNoTracking()
            .Where(i => planIds.Contains(i.InstallmentPlanId) && i.Number == 1)
            .ToDictionaryAsync(i => i.InstallmentPlanId, i => i.DueDate, ct);

        return transactions
            .Where(t => firstDates.ContainsKey(t.Installment!.InstallmentPlanId))
            .Select(t => new InstallmentCandidate(t, firstDates[t.Installment!.InstallmentPlanId]))
            .ToList();
    }

    private sealed record InstallmentCandidate(FinancialTransaction Transaction, DateOnly FirstDate);

    /// <summary>
    /// Pagamentos do cartão que ainda não estão aceitos em outra conciliação, numa janela em torno
    /// das datas da fatura.
    /// </summary>
    private async Task<List<CardPayment>> LoadPaymentCandidatesAsync(ImportCardInvoiceRequest request, CancellationToken ct)
    {
        if (!request.Items.Any(i => i.Amount < 0)) return [];

        var start = request.Items.Min(i => i.Date).AddDays(-SearchWindowDays);
        var end = request.ClosingDate.AddDays(SearchWindowDays);

        return await _db.CardPayments.AsNoTracking()
            .Where(p => p.GrupoId == _currentUser.GrupoId && p.CreditCardId == request.CreditCardId)
            .Where(p => p.PaymentDate >= start && p.PaymentDate <= end)
            .Where(p => !_db.Reconciliations.Any(r => r.CardPaymentId == p.Id && r.AcceptedAt != null))
            .ToListAsync(ct);
    }

    /// <summary>A linha da fatura que registra o pagamento da fatura anterior.</summary>
    /// <summary>
    /// A linha da fatura que registra o pagamento da fatura anterior: negativa e com "PAGAMENTO" ou
    /// "PAGTO" — decidido na importação, no tipo do item (InvoiceTextReader.Classify).
    /// </summary>
    private static bool IsInvoicePayment(CardInvoiceItem item) => item.Kind == CardInvoiceItemKind.Payment;


    private static (CardPayment payment, ReconciliationScorer.ScoreResult score)? FindBestPayment(
        CardInvoiceItem item, DateOnly closingDate, List<CardPayment> candidates, HashSet<Guid> used)
    {
        // A fatura que fecha no mês M mostra o pagamento da fatura de M-1.
        var previousCycle = new DateOnly(closingDate.Year, closingDate.Month, 1).AddMonths(-1);

        (CardPayment payment, ReconciliationScorer.ScoreResult score)? best = null;
        foreach (var payment in candidates)
        {
            if (used.Contains(payment.Id)) continue;
            if (Math.Abs(payment.PaymentDate.DayNumber - item.Date.DayNumber) > SearchWindowDays) continue;

            // A descrição não é comparada: a linha é "PAGAMENTO DE FATURA" e o registro não tem
            // descrição própria. Que a linha é um pagamento já foi decidido por IsInvoicePayment.
            var score = ReconciliationScorer.Score(
                payment.Amount, -item.Amount,
                payment.PaymentDate, item.Date,
                "PAGAMENTO", "PAGAMENTO",
                sameDocumentOrInstallment: payment.CycleReference == previousCycle);

            if (best is null || score.Total > best.Value.score.Total)
                best = (payment, score);
        }
        return best is { score.Total: >= ReconciliationScorer.PossibleMatchThreshold } ? best : null;
    }

    private static (FinancialTransaction transaction, DateOnly firstDate, ReconciliationScorer.ScoreResult score)? FindBestInstallment(
        CardInvoiceItem item, List<InstallmentCandidate> candidates, HashSet<Guid> used)
    {
        var label = InstallmentLabel.TryParse(item.InstallmentLabel);
        if (label is null) return null;

        (FinancialTransaction transaction, DateOnly firstDate, ReconciliationScorer.ScoreResult score)? best = null;
        foreach (var candidate in candidates)
        {
            var installment = candidate.Transaction.Installment!;
            if (used.Contains(candidate.Transaction.Id)) continue;
            if (installment.Number != label.Value.Number || installment.InstallmentPlan!.InstallmentCount != label.Value.Total) continue;
            if (Math.Abs(candidate.FirstDate.DayNumber - item.Date.DayNumber) > SearchWindowDays) continue;

            // A data comparada é a da 1ª parcela com a da compra; a descrição, a do plano (sem
            // o "(4/6)" que o lançamento leva).
            var score = ReconciliationScorer.Score(
                candidate.Transaction.Amount, item.Amount,
                candidate.FirstDate, item.Date,
                installment.InstallmentPlan.Description, item.Description,
                sameDocumentOrInstallment: true);

            if (best is null || score.Total > best.Value.score.Total)
                best = (candidate.Transaction, candidate.FirstDate, score);
        }
        return best is { score.Total: >= ReconciliationScorer.PossibleMatchThreshold } ? best : null;
    }

    private static void ApplyScore(Domain.Entities.Reconciliation reconciliation, ReconciliationScorer.ScoreResult score, string match)
    {
        reconciliation.Score = score.Total;
        reconciliation.AmountDifference = score.AmountDifference;
        reconciliation.DateDifferenceDays = score.DateDifferenceDays;
        reconciliation.DescriptionScore = score.DescriptionScore;
        reconciliation.Status = ReconciliationScorer.StatusFor(score.Total) switch
        {
            "MATCHED" => ReconciliationStatus.Matched,
            "POSSIBLE_MATCH" => ReconciliationStatus.PossibleMatch,
            _ => ReconciliationStatus.Unmatched
        };
        reconciliation.Reason = match;
    }

    private (FinancialTransaction transaction, ReconciliationScorer.ScoreResult score)? FindBestCandidate(
        CardInvoiceItem item, List<FinancialTransaction> candidates, HashSet<Guid> used)
    {
        (FinancialTransaction transaction, ReconciliationScorer.ScoreResult score)? best = null;
        var label = InstallmentLabel.TryParse(item.InstallmentLabel);

        foreach (var transaction in candidates)
        {
            if (used.Contains(transaction.Id)) continue;
            var dateDiff = Math.Abs(transaction.TransactionDate.DayNumber - item.Date.DayNumber);
            if (dateDiff > SearchWindowDays) continue;

            var sameInstallment = label is not null && transaction.Installment?.InstallmentPlan is not null &&
                transaction.Installment.Number == label.Value.Number &&
                transaction.Installment.InstallmentPlan.InstallmentCount == label.Value.Total;
            // "04/06" na fatura nunca é outra parcela: a 1ª de um plano de 10 comprada no mesmo dia
            // tem data e valor iguais e passaria aqui.
            if (label is not null && transaction.Installment is not null && !sameInstallment) continue;

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
        if (reconciliation.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
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
        r.Reason,
        r.CardPaymentId,
        r.AcceptedAt is not null,
        r.CardInvoiceItem?.CardLastFour,
        r.CardInvoiceItem?.Kind.ToString(),
        r.CardInvoiceItem?.ForeignAmount,
        r.CardInvoiceItem?.ForeignCurrency,
        r.CardInvoiceItem?.ExchangeRate,
        r.CardInvoiceItem?.ExchangeRateEstimated ?? false,
        r.CardInvoiceItem?.OriginalAmount,
        r.CardInvoiceItem?.OriginalCurrency,
        r.CardInvoiceItem?.IssuedCardId,
        r.CardInvoiceItem?.RelatedItemId
    );
}
