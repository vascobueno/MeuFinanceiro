using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Accounts;

public record CreateAccountRequest(string Name, string? Bank, AccountType Type, decimal InitialBalance);
public record UpdateAccountRequest(string Name, string? Bank, AccountType Type, decimal InitialBalance, bool IsActive);

public record AccountResponse(
    Guid Id,
    string Name,
    string? Bank,
    AccountType Type,
    decimal InitialBalance,
    decimal CurrentBalance,
    bool IsActive);
