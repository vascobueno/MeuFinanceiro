using MeuFinanceiro.Application.Common;
using MeuFinanceiro.Application.Common.Exceptions;
using MeuFinanceiro.Application.Common.Interfaces;
using MeuFinanceiro.Domain.Entities;
using MeuFinanceiro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MeuFinanceiro.Application.Accounts;

public class AccountService : IAccountService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AccountService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AccountResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Accounts.AsNoTracking().Where(a => a.HouseholdId == _currentUser.HouseholdId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // ToLower() dos dois lados: ver comentário equivalente em MerchantService.
            var normalizedSearch = request.Search.ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(normalizedSearch) || (a.Bank != null && a.Bank.ToLower().Contains(normalizedSearch)));
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "bank" => request.IsDescending ? query.OrderByDescending(a => a.Bank) : query.OrderBy(a => a.Bank),
            "type" => request.IsDescending ? query.OrderByDescending(a => a.Type) : query.OrderBy(a => a.Type),
            _ => request.IsDescending ? query.OrderByDescending(a => a.Name) : query.OrderBy(a => a.Name)
        };

        var totalItems = await query.CountAsync(ct);
        var accounts = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var items = new List<AccountResponse>(accounts.Count);
        foreach (var a in accounts)
            items.Add(await ToResponseAsync(a, ct));

        return PagedResult<AccountResponse>.Create(items, request.Page, request.PageSize, totalItems);
    }

    public async Task<AccountResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);
        return await ToResponseAsync(account, ct);
    }

    public async Task<AccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        var account = new Account
        {
            HouseholdId = _currentUser.HouseholdId,
            Name = request.Name,
            Bank = request.Bank,
            Type = request.Type,
            InitialBalance = request.InitialBalance,
            CreatedBy = _currentUser.UserId
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(account, ct);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);
        account.Name = request.Name;
        account.Bank = request.Bank;
        account.Type = request.Type;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return await ToResponseAsync(account, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);

        var hasTransactions = await _db.FinancialTransactions.AnyAsync(t => t.AccountId == id, ct);
        if (hasTransactions)
            throw new BusinessRuleException(
                "conta-possui-lancamentos",
                "Não é possível excluir uma conta que já possui lançamentos. Desative-a em vez de excluir.");

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Account> FindOwnedAsync(Guid id, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException(nameof(Account), id);

        if (account.HouseholdId != _currentUser.HouseholdId)
            throw new ForbiddenException();

        return account;
    }

    private async Task<AccountResponse> ToResponseAsync(Account a, CancellationToken ct)
    {
        var sum = await _db.FinancialTransactions
            .Where(t => t.AccountId == a.Id && t.Status != TransactionStatus.Pending)
            .SumAsync(t => (decimal?)(t.Direction == TransactionDirection.Credit ? t.Amount : -t.Amount), ct) ?? 0m;

        return new AccountResponse(a.Id, a.Name, a.Bank, a.Type, a.InitialBalance, a.InitialBalance + sum, a.IsActive);
    }
}
