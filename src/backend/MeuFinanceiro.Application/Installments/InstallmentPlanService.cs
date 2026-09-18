using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Application.Transactions;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Installments;

/// <summary>
/// Tela Parcelamento (docs/screens/parcelamento). Na criação não duplica a lógica de parcelas:
/// delega para o mesmo caminho já usado/testado em TransactionService (InstallmentCalculator +
/// criação de InstallmentPlan/Installment/FinancialTransaction), só que exposto como recurso
/// próprio (POST /api/v1/installment-plans), conforme pedido pela tela dedicada em vez de
/// passar pelo formulário geral de lançamento.
/// </summary>
public class InstallmentPlanService : IInstallmentPlanService
{
    private readonly ITransactionService _transactionService;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InstallmentPlanService(ITransactionService transactionService, IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _transactionService = transactionService;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<InstallmentPlanResponse> CreateAsync(CreateInstallmentPlanRequest request, CancellationToken ct = default)
    {
        var transactionRequest = new CreateTransactionRequest(
            request.FirstDueDate,
            request.Description,
            request.CategoryId,
            SubcategoryId: null,
            request.AccountId,
            request.CreditCardId,
            DestinationAccountId: null,
            TransactionType.Expense,
            TransactionDirection.Debit,
            Amount: request.Amount,
            Notes: null,
            Installment: new InstallmentRequest(request.Mode, request.Amount, request.InstallmentCount, request.FirstDueDate),
            Recurring: null
        );

        var created = await _transactionService.CreateAsync(transactionRequest, ct);
        var amounts = created.Select(t => t.Amount).ToList();

        // O plano é criado lá dentro; o id volta pelas parcelas — é o que permite anexar arquivo
        // ao parcelamento recém-criado sem uma segunda consulta.
        var planId = created.Select(t => t.InstallmentPlanId).FirstOrDefault(id => id.HasValue)
            ?? throw new InvalidOperationException("Parcelamento criado sem plano associado.");

        return new InstallmentPlanResponse(planId, amounts.Sum(), amounts.Count, amounts);
    }

    public async Task<PagedResult<InstallmentPlanListItemResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.InstallmentPlans
            .AsNoTracking()
            .Include(p => p.Installments)
            .Where(p => p.GrupoId == _currentUser.GrupoId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var normalizedSearch = request.Search.ToLower();
            query = query.Where(p => p.Description.ToLower().Contains(normalizedSearch));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "description" => request.IsDescending ? query.OrderByDescending(p => p.Description) : query.OrderBy(p => p.Description),
            "totalamount" => request.IsDescending ? query.OrderByDescending(p => p.TotalAmount) : query.OrderBy(p => p.TotalAmount),
            "installmentcount" => request.IsDescending ? query.OrderByDescending(p => p.InstallmentCount) : query.OrderBy(p => p.InstallmentCount),
            _ => request.IsDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
        };

        var totalItems = await query.CountAsync(ct);
        var plans = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var origins = await LoadOriginsAsync(plans, ct);

        var items = plans.Select(plan =>
        {
            var installments = plan.Installments.OrderBy(i => i.Number).ToList();
            var origin = origins.GetValueOrDefault(plan.Id);

            var paidCount = installments.Count(i => origin.StatusByInstallment.GetValueOrDefault(i.Id) == TransactionStatus.Reconciled);
            var next = installments.FirstOrDefault(i => origin.StatusByInstallment.GetValueOrDefault(i.Id) != TransactionStatus.Reconciled);

            return new InstallmentPlanListItemResponse(
                plan.Id,
                plan.Description,
                plan.Mode,
                plan.TotalAmount,
                plan.InstallmentCount,
                paidCount,
                origin.AccountName,
                origin.CreditCardName,
                installments.Count > 0 ? installments[0].DueDate : default,
                installments.Count > 0 ? installments[^1].DueDate : default,
                next?.Number,
                next?.DueDate,
                next?.Amount
            );
        }).ToList();

        return PagedResult<InstallmentPlanListItemResponse>.Create(items, request.Page, request.PageSize, totalItems);
    }

    public async Task<InstallmentPlanDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _db.InstallmentPlans
            .AsNoTracking()
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(InstallmentPlan), id);

        if (plan.GrupoId != _currentUser.GrupoId) throw new ForbiddenException();

        var origin = (await LoadOriginsAsync(new[] { plan }, ct)).GetValueOrDefault(plan.Id);
        var installments = plan.Installments.OrderBy(i => i.Number).ToList();

        var items = installments.Select(i => new InstallmentItemResponse(
            i.Id,
            i.Number,
            i.Amount,
            i.DueDate,
            origin.StatusByInstallment.TryGetValue(i.Id, out var status) ? status : null,
            i.FinancialTransactionId
        )).ToList();

        return new InstallmentPlanDetailResponse(
            plan.Id,
            plan.Description,
            plan.Mode,
            plan.TotalAmount,
            plan.InstallmentCount,
            items.Count(i => i.Status == TransactionStatus.Reconciled),
            origin.AccountName,
            origin.CreditCardName,
            items
        );
    }

    private readonly record struct PlanOrigin(string? AccountName, string? CreditCardName, Dictionary<Guid, TransactionStatus> StatusByInstallment)
    {
        public static PlanOrigin Empty => new(null, null, new Dictionary<Guid, TransactionStatus>());
    }

    /// <summary>
    /// Origem (conta/cartão) e andamento de cada plano vêm dos lançamentos gerados, não do
    /// próprio plano. Uma query para a página inteira, em vez de uma por plano.
    /// </summary>
    private async Task<Dictionary<Guid, PlanOrigin>> LoadOriginsAsync(IReadOnlyList<InstallmentPlan> plans, CancellationToken ct)
    {
        var installmentIds = plans.SelectMany(p => p.Installments).Select(i => i.Id).ToList();

        var transactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.CreditCard)
            .Where(t => t.InstallmentId != null && installmentIds.Contains(t.InstallmentId!.Value))
            .Select(t => new { InstallmentId = t.InstallmentId!.Value, t.Status, AccountName = t.Account!.Name, CreditCardName = t.CreditCard!.Name })
            .ToListAsync(ct);

        var byInstallment = transactions.ToDictionary(t => t.InstallmentId);

        return plans.ToDictionary(plan => plan.Id, plan =>
        {
            var linked = plan.Installments
                .Select(i => byInstallment.GetValueOrDefault(i.Id))
                .Where(t => t is not null)
                .ToList();

            if (linked.Count == 0) return PlanOrigin.Empty;

            return new PlanOrigin(
                linked[0]!.AccountName,
                linked[0]!.CreditCardName,
                linked.ToDictionary(t => t!.InstallmentId, t => t!.Status));
        });
    }
}
