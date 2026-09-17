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
    private readonly IAccountBalanceService _balances;

    public AccountService(IApplicationDbContext db, ICurrentUserService currentUser, IAccountBalanceService balances)
    {
        _db = db;
        _currentUser = currentUser;
        _balances = balances;
    }

    public async Task<PagedResult<AccountResponse>> GetAllAsync(PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Accounts.AsNoTracking().Where(a => a.GrupoId == _currentUser.GrupoId);

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

        // Uma consulta para a página inteira, não uma por conta.
        var balances = await _balances.GetBalancesAsync(accounts.Select(a => a.Id).ToList(), ct);
        var items = accounts.Select(a => ToResponse(a, balances[a.Id])).ToList();

        return PagedResult<AccountResponse>.Create(items, request.Page, request.PageSize, totalItems);
    }

    public async Task<AccountResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);
        return await ToResponseWithBalanceAsync(account, ct);
    }

    public async Task<AccountResponse> CreateAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        var account = new Account
        {
            GrupoId = _currentUser.GrupoId,
            Name = request.Name,
            Bank = request.Bank,
            Type = request.Type,
            InitialBalance = request.InitialBalance,
            CreatedBy = _currentUser.UserId
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return await ToResponseWithBalanceAsync(account, ct);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);
        account.Name = request.Name;
        account.Bank = request.Bank;
        account.Type = request.Type;
        account.InitialBalance = request.InitialBalance;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);
        return await ToResponseWithBalanceAsync(account, ct);
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

        if (account.GrupoId != _currentUser.GrupoId)
            throw new ForbiddenException();

        return account;
    }

    private async Task<AccountResponse> ToResponseWithBalanceAsync(Account a, CancellationToken ct)
    {
        var balances = await _balances.GetBalancesAsync(new[] { a.Id }, ct);
        return ToResponse(a, balances[a.Id]);
    }

    private static AccountResponse ToResponse(Account a, decimal movements) =>
        new(a.Id, a.Name, a.Bank, a.Type, a.InitialBalance, a.InitialBalance + movements, a.IsActive);
}
