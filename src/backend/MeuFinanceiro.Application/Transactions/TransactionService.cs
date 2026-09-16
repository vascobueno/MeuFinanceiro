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

    public TransactionService(IApplicationDbContext db, ICurrentUserService currentUser, IMerchantService merchantService)
    {
        _db = db;
        _currentUser = currentUser;
        _merchantService = merchantService;
    }

    public async Task<PagedResult<TransactionResponse>> GetAllAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var query = _db.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Include(t => t.CreditCard)
            .Include(t => t.Installment!).ThenInclude(i => i.InstallmentPlan)
            .Where(t => t.GrupoId == _currentUser.GrupoId);

        if (filter.StartDate is not null) query = query.Where(t => t.TransactionDate >= filter.StartDate);
        if (filter.EndDate is not null) query = query.Where(t => t.TransactionDate <= filter.EndDate);
        if (filter.CategoryId is not null) query = query.Where(t => t.CategoryId == filter.CategoryId);
        if (filter.AccountId is not null) query = query.Where(t => t.AccountId == filter.AccountId);
        if (filter.CreditCardId is not null) query = query.Where(t => t.CreditCardId == filter.CreditCardId);
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

        return PagedResult<TransactionResponse>.Create(results.Select(t => ToResponse(t)).ToList(), filter.Page, filter.PageSize, totalItems);
    }

    public async Task<TransactionResponse> GetByIdAsync(Guid id, CancellationToken ct = default) => ToResponse(await FindOwnedAsync(id, ct));

    public async Task<IReadOnlyList<TransactionResponse>> CreateAsync(CreateTransactionRequest request, CancellationToken ct = default)
    {
        ValidateOrigin(request.AccountId, request.CreditCardId, request.Type);

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
        transaction.UpdatedAt = DateTime.UtcNow;
        transaction.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return ToResponse(transaction);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await FindOwnedAsync(id, ct);
        EnsureNotReconciled(transaction);

        _db.FinancialTransactions.Remove(transaction);
        await _db.SaveChangesAsync(ct);
    }

    // ----------------- helpers -----------------

    private static void ValidateOrigin(Guid? accountId, Guid? creditCardId, TransactionType type)
    {
        if (type == TransactionType.Transfer) return; // validado em CreateTransferAsync

        if (accountId is null && creditCardId is null)
            throw new BusinessRuleException("origem-obrigatoria", "Informe uma conta ou um cartão de origem para o lançamento.");

        if (accountId is not null && creditCardId is not null)
            throw new BusinessRuleException("origem-ambigua", "Um lançamento pertence a uma conta OU a um cartão, nunca aos dois (regra 3).");
    }

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
            responses.Add(ToResponse(transaction, plan.InstallmentCount));
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

    private FinancialTransaction BuildTransaction(CreateTransactionRequest request, Guid? merchantId) => new()
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
        CreatedBy = _currentUser.UserId
    };

    private async Task<FinancialTransaction> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var transaction = await _db.FinancialTransactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Include(t => t.CreditCard)
            .Include(t => t.Installment!).ThenInclude(i => i.InstallmentPlan)
            .FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(FinancialTransaction), id);

        if (transaction.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();
        return transaction;
    }

    private static TransactionResponse ToResponse(FinancialTransaction t, int? installmentCount = null) => new(
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
        t.RecurringTransactionId,
        t.Notes
    );
}
