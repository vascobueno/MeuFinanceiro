using MeuFinanceiro.Application.Attachments;
using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Installments;
using MeuFinanceiro.Application.Merchants;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Transactions;

/// <summary>
/// Regras aplicadas aqui (docs/05-regras-negocio.md e .github/copilot-instructions.md):
/// - 9: transferência entre contas próprias não é receita/despesa.
/// - 12: lançamento conciliado não é alterado/excluído automaticamente.
/// - 15/16/17: parcelamento aceita valor total OU valor da parcela; soma fecha o principal
///   exatamente; arredondamento fica na última parcela.
/// - 20: valores em decimal.
/// - 21: regras de negócio validadas na API (nesta camada).
/// </summary>
public class TransactionService : ITransactionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMerchantService _merchantService;
    private readonly IAttachmentService _attachmentService;

    public TransactionService(IApplicationDbContext db, ICurrentUserService currentUser, IMerchantService merchantService, IAttachmentService attachmentService)
    {
        _db = db;
        _currentUser = currentUser;
        _merchantService = merchantService;
        _attachmentService = attachmentService;
    }

    public async Task<PagedResult<TransactionResponse>> GetAllAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var query = _db.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Include(t => t.CreditCard)
            .Include(t => t.IssuedCard)
            .Include(t => t.Installment!).ThenInclude(i => i.InstallmentPlan)
            .Where(t => t.GrupoId == _currentUser.GrupoId);

        if (filter.StartDate is not null) query = query.Where(t => t.TransactionDate >= filter.StartDate);
        if (filter.EndDate is not null) query = query.Where(t => t.TransactionDate <= filter.EndDate);
        if (filter.CategoryId is not null) query = query.Where(t => t.CategoryId == filter.CategoryId);
        if (filter.AccountId is not null) query = query.Where(t => t.AccountId == filter.AccountId);
        if (filter.CreditCardId is not null) query = query.Where(t => t.CreditCardId == filter.CreditCardId);
        if (filter.IssuedCardId is not null) query = query.Where(t => t.IssuedCardId == filter.IssuedCardId);
        // Origin responde "de qualquer conta" / "de qualquer cartão" — os filtros por id acima
        // só respondem "desta conta" / "deste cartão".
        if (filter.Origin == TransactionOrigin.Account) query = query.Where(t => t.AccountId != null);
        if (filter.Origin == TransactionOrigin.Card) query = query.Where(t => t.CreditCardId != null);
        if (filter.Type is not null) query = query.Where(t => t.Type == filter.Type);
        if (filter.Status is not null) query = query.Where(t => t.Status == filter.Status);
        if (filter.MinAmount is not null) query = query.Where(t => t.Amount >= filter.MinAmount);
        if (filter.MaxAmount is not null) query = query.Where(t => t.Amount <= filter.MaxAmount);
        // ToLower() dos dois lados: ver comentário equivalente em MerchantService sobre a
        // diferença de collation padrão entre SQL Server e Postgres.
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var normalizedSearch = filter.Search.ToLower();
            query = query.Where(t => t.Description.ToLower().Contains(normalizedSearch));
        }

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "amount" => filter.IsDescending ? query.OrderByDescending(t => t.Amount) : query.OrderBy(t => t.Amount),
            "description" => filter.IsDescending ? query.OrderByDescending(t => t.Description) : query.OrderBy(t => t.Description),
            "status" => filter.IsDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            _ => filter.IsDescending ? query.OrderByDescending(t => t.TransactionDate) : query.OrderBy(t => t.TransactionDate)
        };

        var totalItems = await query.CountAsync(ct);
        var results = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);

        var attachmentCounts = await CountAttachmentsAsync(results, ct);
        var invoiceFinals = await InvoiceFinalsAsync(results, ct);

        return PagedResult<TransactionResponse>.Create(
            results.Select(t => ToResponse(t, attachmentCount: attachmentCounts.GetValueOrDefault(t.Id))
                with { InvoiceCardLastFour = invoiceFinals.GetValueOrDefault(t.Id) }).ToList(),
            filter.Page, filter.PageSize, totalItems);
    }

    public async Task<TransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default) => ToResponse(await FindOwnedAsync(id, ct));

    public async Task<IReadOnlyList<TransactionResponse>> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default)
    {
        TransactionOriginRules.Validate(request.AccountId, request.CreditCardId, request.Type);
        _issuedCardForCreate = request.Type == TransactionType.Transfer
            ? null
            : await EnsureIssuedCardFitsAsync(request.IssuedCardId, request.AccountId, request.CreditCardId, ct);

        if (request.Type == TransactionType.Transfer)
            return await CreateTransferAsync(request, ct);

        if (request.Installment is not null)
            return await CreateInstallmentPlanAsync(request, request.Installment, ct);

        if (request.Recurring is not null)
            return await CreateRecurringAsync(request, request.Recurring, ct);

        var merchantId = await _merchantService.GetOrCreateAndTouchAsync(request.Description, ct);
        var transaction = BuildTransaction(request, merchantId);

        _db.FinancialTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        return new[] { ToResponse(transaction) };
    }

    public async Task<TransactionResponse> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct = default)
    {
        var transaction = await FindOwnedAsync(id, ct);
        EnsureNotReconciled(transaction);

        transaction.TransactionDate = request.TransactionDate;
        transaction.Description = request.Description;
        transaction.CategoryId = request.CategoryId;
        transaction.SubcategoryId = request.SubcategoryId;
        transaction.AccountId = request.AccountId;
        transaction.CreditCardId = request.CreditCardId;
        transaction.Amount = request.Amount;
        transaction.Direction = request.Direction;
        transaction.Status = request.Status;
        transaction.Notes = request.Notes;
        ForeignPurchase.Apply(transaction, request.Foreign);
        var issuedCard = await EnsureIssuedCardFitsAsync(request.IssuedCardId, request.AccountId, request.CreditCardId, ct);
        transaction.IssuedCardId = issuedCard?.Id;
        transaction.IssuedCard = issuedCard;
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToResponse(transaction);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await FindOwnedAsync(id, ct);
        EnsureNotReconciled(transaction);

        // Antes de remover o lançamento: o cascade do banco apaga as linhas de anexo, mas não os
        // objetos no storage — eles ficariam órfãos, ocupando espaço sem nada os referenciar.
        await _attachmentService.DeleteForOwnerAsync(
            new AttachmentOwner(AttachmentOwnerKind.Transaction, transaction.Id), ct);

        _db.FinancialTransactions.Remove(transaction);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------- helpers -----------------

    private static void EnsureNotReconciled(FinancialTransaction transaction)
    {
        if (transaction.Status == TransactionStatus.Reconciled)
            throw new BusinessRuleException(
                "lancamento-conciliado",
                "Lançamentos já conciliados não podem ser alterados/excluídos automaticamente (regra 12). Desfaça a conciliação primeiro.");
    }

    private async Task<IReadOnlyList<TransactionResponse>> CreateTransferAsync(CreateTransactionRequest request, CancellationToken ct)
    {
        if (request.AccountId is null || request.DestinationAccountId is null)
            throw new BusinessRuleException("transferencia-invalida", "Transferências exigem conta de origem e conta de destino.");

        if (request.AccountId == request.DestinationAccountId)
            throw new BusinessRuleException("transferencia-invalida", "Conta de origem e destino não podem ser a mesma.");

        var outgoing = BuildTransaction(request with { AccountId = request.AccountId, CreditCardId = null }, merchantId: null);
        outgoing.Direction = TransactionDirection.Debit;

        var incoming = BuildTransaction(request with { AccountId = request.DestinationAccountId, CreditCardId = null }, merchantId: null);
        incoming.Direction = TransactionDirection.Credit;

        outgoing.RelatedTransactionId = incoming.Id;
        incoming.RelatedTransactionId = outgoing.Id;

        _db.FinancialTransactions.AddRange(outgoing, incoming);
        await _db.SaveChangesAsync(ct);

        return new[] { ToResponse(outgoing), ToResponse(incoming) };
    }

    private async Task<IReadOnlyList<TransactionResponse>> CreateInstallmentPlanAsync(CreateTransactionRequest request, InstallmentRequest installmentRequest, CancellationToken ct)
    {
        if (installmentRequest.InstallmentCount < 1)
            throw new BusinessRuleException("parcelamento-invalido", "A quantidade de parcelas deve ser maior que zero.");

        decimal totalAmount;
        List<decimal> installmentAmounts;

        if (installmentRequest.Mode == InstallmentMode.TotalAmount)
        {
            totalAmount = installmentRequest.Amount;
            installmentAmounts = InstallmentCalculator.SplitTotal(totalAmount, installmentRequest.InstallmentCount);
        }
        else
        {
            installmentAmounts = InstallmentCalculator.FromInstallmentAmount(installmentRequest.Amount, installmentRequest.InstallmentCount);
            totalAmount = installmentAmounts.Sum();
        }

        // Regra 16: a soma das parcelas deve fechar exatamente o principal.
        if (installmentAmounts.Sum() != totalAmount)
            throw new BusinessRuleException("parcelamento-invalido", "Soma das parcelas não fecha com o valor principal.");

        var plan = new InstallmentPlan
        {
            GrupoId = _currentUser.GrupoId,
            Description = request.Description,
            Mode = installmentRequest.Mode,
            TotalAmount = totalAmount,
            InstallmentCount = installmentRequest.InstallmentCount,
            CreatedBy = _currentUser.UserId
        };
        _db.InstallmentPlans.Add(plan);

        var merchantId = await _merchantService.GetOrCreateAndTouchAsync(request.Description, ct);
        var responses = new List<TransactionResponse>();

        for (var i = 0; i < installmentRequest.InstallmentCount; i++)
        {
            var installment = new Installment
            {
                InstallmentPlanId = plan.Id,
                Number = i + 1,
                Amount = installmentAmounts[i],
                DueDate = installmentRequest.FirstDueDate.AddMonths(i)
            };
            _db.Installments.Add(installment);

            // O "(n/total)" vai na própria descrição gravada, não só na montagem da tela de
            // Lançamentos: assim a parcela continua identificável na fatura do cartão, na
            // conciliação e em qualquer outro lugar que mostre a descrição. O merchantId acima
            // é resolvido a partir da descrição base, então o autocomplete não recebe sufixos.
            var transaction = BuildTransaction(
                request with
                {
                    TransactionDate = installment.DueDate,
                    Amount = installment.Amount,
                    Description = $"{request.Description} ({installment.Number}/{installmentRequest.InstallmentCount})"
                },
                merchantId);
            transaction.InstallmentId = installment.Id;
            installment.FinancialTransactionId = transaction.Id;

            _db.FinancialTransactions.Add(transaction);
            responses.Add(ToResponse(transaction, plan.InstallmentCount, plan.Id));
        }

        await _db.SaveChangesAsync(ct);
        return responses;
    }

    private async Task<IReadOnlyList<TransactionResponse>> CreateRecurringAsync(CreateTransactionRequest request, RecurringRequest recurringRequest, CancellationToken ct)
    {
        var recurring = new RecurringTransaction
        {
            GrupoId = _currentUser.GrupoId,
            Description = request.Description,
            AccountId = request.AccountId,
            CreditCardId = request.CreditCardId,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Frequency = recurringRequest.Frequency,
            StartDate = request.TransactionDate,
            EndDate = recurringRequest.EndDate,
            CreatedBy = _currentUser.UserId
        };
        _db.RecurringTransactions.Add(recurring);

        var version = new RecurringTransactionVersion
        {
            RecurringTransactionId = recurring.Id,
            EffectiveFrom = request.TransactionDate,
            Amount = request.Amount
        };
        _db.RecurringTransactionVersions.Add(version);

        var merchantId = await _merchantService.GetOrCreateAndTouchAsync(request.Description, ct);
        var firstOccurrence = BuildTransaction(request, merchantId);
        firstOccurrence.RecurringTransactionId = recurring.Id;

        _db.FinancialTransactions.Add(firstOccurrence);
        await _db.SaveChangesAsync(ct);

        return new[] { ToResponse(firstOccurrence) };
    }

    private FinancialTransaction BuildTransaction(CreateTransactionRequest request, Guid? merchantId) => new FinancialTransaction
    {
        GrupoId = _currentUser.GrupoId,
        TransactionDate = request.TransactionDate,
        Description = request.Description,
        MerchantId = merchantId,
        CategoryId = request.CategoryId,
        SubcategoryId = request.SubcategoryId,
        AccountId = request.AccountId,
        CreditCardId = request.CreditCardId,
        Type = request.Type,
        Direction = request.Direction,
        Amount = request.Amount,
        Status = TransactionStatus.Pending,
        Notes = request.Notes,
        // Parcelas herdam do pedido; a transferência não chega aqui com cartão (validado acima).
        IssuedCardId = request.Type == TransactionType.Transfer ? null : _issuedCardForCreate?.Id,
        IssuedCard = request.Type == TransactionType.Transfer ? null : _issuedCardForCreate,
        CreatedBy = _currentUser.UserId
    }.WithForeign(request.Installment is null && request.Recurring is null ? request.Foreign : null);

    /// <summary>
    /// O cartão usado tem de ser do grupo de usuários e combinar com a origem: no lançamento do
    /// cartão de crédito, um cartão daquele grupo de fatura; no da conta, um cartão com débito nela.
    /// </summary>
    /// <remarks>Devolve o cartão rastreado, para a resposta trazer o final sem outra consulta.</remarks>
    private async Task<IssuedCard?> EnsureIssuedCardFitsAsync(Guid? issuedCardId, Guid? accountId, Guid? creditCardId, CancellationToken ct)
    {
        if (issuedCardId is not { } id) return null;
        var card = await _db.IssuedCards.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(IssuedCard), id);
        if (card.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        if (creditCardId is not null && card.CreditCardId != creditCardId)
            throw new BusinessRuleException("cartao-de-outra-fatura", "Este cartão não é do cartão de crédito do lançamento.");
        if (accountId is not null && card.AccountId != accountId)
            throw new BusinessRuleException("cartao-sem-debito-na-conta", "Este cartão não tem débito na conta do lançamento.");
        return card;
    }

    /// <summary>
    /// O cartão validado em CreateAsync, para BuildTransaction pôr em cada lançamento criado —
    /// inclusive em cada parcela. O serviço é por requisição, então o campo vive só nela.
    /// </summary>
    private IssuedCard? _issuedCardForCreate;

    private async Task<FinancialTransaction> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var transaction = await _db.FinancialTransactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Include(t => t.CreditCard)
            .Include(t => t.IssuedCard)
            .Include(t => t.Installment!).ThenInclude(i => i.InstallmentPlan)
            .FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(FinancialTransaction), id);

        if (transaction.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return transaction;
    }

    /// <summary>
    /// Quantos anexos cada lançamento da página tem, em DUAS consultas agregadas para a página
    /// inteira — nunca uma por linha. A rodada de performance anterior derrubou exatamente esse
    /// tipo de N+1 em /accounts (12 consultas para 3); reintroduzi-lo aqui apagaria o ganho.
    ///
    /// Uma parcela herda a contagem do plano: a nota da compra foi anexada ao parcelamento e vale
    /// para todas as parcelas, então marcar só o plano deixaria as 12 linhas parecendo sem
    /// documento na tela de Lançamentos.
    /// </summary>
    private async Task<Dictionary<Guid, int>> CountAttachmentsAsync(
        IReadOnlyCollection<FinancialTransaction> transactions, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        if (transactions.Count == 0) return counts;

        var transactionIds = transactions.Select(t => t.Id).ToList();
        var porLancamento = await _db.Attachments
            .AsNoTracking()
            .Where(a => a.GrupoId == _currentUser.GrupoId
                     && a.FinancialTransactionId != null
                     && transactionIds.Contains(a.FinancialTransactionId.Value))
            .GroupBy(a => a.FinancialTransactionId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Total, ct);

        var planIds = transactions
            .Where(t => t.Installment is not null)
            .Select(t => t.Installment!.InstallmentPlanId)
            .Distinct()
            .ToList();

        var porPlano = planIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.Attachments
                .AsNoTracking()
                .Where(a => a.GrupoId == _currentUser.GrupoId
                         && a.InstallmentPlanId != null
                         && planIds.Contains(a.InstallmentPlanId.Value))
                .GroupBy(a => a.InstallmentPlanId!.Value)
                .Select(g => new { Id = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Total, ct);

        foreach (var transaction in transactions)
        {
            var total = porLancamento.GetValueOrDefault(transaction.Id);
            if (transaction.Installment is not null)
                total += porPlano.GetValueOrDefault(transaction.Installment.InstallmentPlanId);

            if (total > 0) counts[transaction.Id] = total;
        }

        return counts;
    }

    // installmentCount/installmentPlanId vêm por parâmetro no caminho de criação: ali as entidades
    /// <summary>
    /// Para o lançamento de cartão sem o cartão usado: o final da seção da fatura com que ele foi
    /// conciliado. Só conciliação aceita — a sugestão pode ser de outra compra. Só da página.
    /// </summary>
    private async Task<Dictionary<Guid, string>> InvoiceFinalsAsync(List<FinancialTransaction> page, CancellationToken ct)
    {
        var ids = page.Where(t => t.IssuedCardId is null && t.CreditCardId is not null).Select(t => t.Id).ToList();
        if (ids.Count == 0) return new();
        var rows = await _db.Reconciliations.AsNoTracking()
            .Where(r => r.GrupoId == _currentUser.GrupoId && r.AcceptedAt != null
                        && r.FinancialTransactionId != null && ids.Contains(r.FinancialTransactionId.Value)
                        && r.CardInvoiceItem != null && r.CardInvoiceItem.CardLastFour != null)
            .Select(r => new { Id = r.FinancialTransactionId!.Value, Final = r.CardInvoiceItem!.CardLastFour! })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Final);
    }

    // acabaram de ser montadas em memória e a navegação t.Installment ainda não está carregada,
    // então ler o plano por ela devolveria null exatamente quando a tela precisa do id.
    private static TransactionResponse ToResponse(FinancialTransaction t, int? installmentCount = null, Guid? installmentPlanId = null, int attachmentCount = 0) => new(
        t.Id,
        t.TransactionDate,
        t.Description,
        t.MerchantId,
        t.CategoryId,
        t.Category?.Name,
        t.SubcategoryId,
        t.AccountId,
        t.Account?.Name,
        t.CreditCardId,
        t.CreditCard?.Name,
        t.Type,
        t.Direction,
        t.Amount,
        t.Status,
        t.InstallmentId,
        t.Installment?.Number,
        installmentCount ?? t.Installment?.InstallmentPlan?.InstallmentCount,
        installmentPlanId ?? t.Installment?.InstallmentPlanId,
        t.RecurringTransactionId,
        t.Notes,
        attachmentCount,
        ForeignPurchase.ToResponse(t),
        t.IssuedCardId,
        t.IssuedCard?.LastFour,
        t.IssuedCard?.Kind
    );
}
