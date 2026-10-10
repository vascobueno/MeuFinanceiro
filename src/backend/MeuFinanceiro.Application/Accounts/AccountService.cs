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
        var details = BankDetails.Parse(request.BankCode, request.Agency, request.AgencyDigit, request.AccountNumber, request.AccountDigit);
        EnsureValidLimit(request.OverdraftLimit);
        await EnsureNotDuplicateAsync(details, null, ct);

        var account = new Account
        {
            GrupoId = _currentUser.GrupoId,
            Name = request.Name,
            Bank = request.Bank,
            Type = request.Type,
            InitialBalance = request.InitialBalance,
            OverdraftLimit = request.OverdraftLimit,
            CreatedBy = _currentUser.UserId
        };
        Apply(account, details);

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return await ToResponseWithBalanceAsync(account, ct);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var account = await FindOwnedAsync(id, ct);
        var details = BankDetails.Parse(request.BankCode, request.Agency, request.AgencyDigit, request.AccountNumber, request.AccountDigit);
        EnsureValidLimit(request.OverdraftLimit);
        await EnsureNotDuplicateAsync(details, id, ct);

        Apply(account, details);
        account.OverdraftLimit = request.OverdraftLimit;
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

        // Cartões com débito nesta conta: os só de débito vão junto; o múltiplo fica só de crédito.
        await Cards.CardService.DetachOrRemoveIssuedCardsAsync(_db, _db.IssuedCards.Where(c => c.AccountId == id),
            c => c.CreditCardId is not null, c => c.AccountId = null, ct);

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(Account account, BankDetails details)
    {
        account.BankCode = details.BankCode;
        account.Agency = details.Agency;
        account.AgencyDigit = details.AgencyDigit;
        account.AccountNumber = details.AccountNumber;
        account.AccountDigit = details.AccountDigit;
    }

    private static void EnsureValidLimit(decimal limit)
    {
        if (limit < 0)
            throw new BusinessRuleException("limite-invalido", "O limite do cheque especial não pode ser negativo.");
    }

    /// <summary>
    /// A mesma agência e conta não podem ser cadastradas duas vezes no grupo — senão o extrato
    /// (e o OFX) não saberia em qual delas entrar. Também há índice único; a checagem aqui dá a
    /// mensagem e cobre o banco em memória dos testes, que ignora índices únicos.
    /// </summary>
    private async Task EnsureNotDuplicateAsync(BankDetails details, Guid? exceptId, CancellationToken ct)
    {
        if (details.IsEmpty) return;

        var existing = await _db.Accounts.AsNoTracking()
            .Where(a => a.GrupoId == _currentUser.GrupoId && a.Id != exceptId
                        && a.BankCode == details.BankCode && a.Agency == details.Agency
                        && a.AccountNumber == details.AccountNumber && a.AccountDigit == details.AccountDigit)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            throw new BusinessRuleException("conta-duplicada",
                $"A agência e conta {details.Display} já estão cadastradas em \"{existing}\".");
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
        new(a.Id, a.Name, a.Bank, a.Type, a.InitialBalance, a.InitialBalance + movements, a.IsActive,
            a.BankCode, a.Agency, a.AgencyDigit, a.AccountNumber, a.AccountDigit, a.OverdraftLimit,
            a.InitialBalance + movements + a.OverdraftLimit);
}
