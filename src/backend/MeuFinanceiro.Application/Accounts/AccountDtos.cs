using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Accounts;

// Os campos novos têm padrão para que clientes que ainda não os mandam continuem funcionando.
public record CreateAccountRequest(string Name, string? Bank, AccountType Type, decimal InitialBalance,
    string? BankCode = null, string? Agency = null, string? AgencyDigit = null,
    string? AccountNumber = null, string? AccountDigit = null, decimal OverdraftLimit = 0);

public record UpdateAccountRequest(string Name, string? Bank, AccountType Type, decimal InitialBalance, bool IsActive,
    string? BankCode = null, string? Agency = null, string? AgencyDigit = null,
    string? AccountNumber = null, string? AccountDigit = null, decimal OverdraftLimit = 0);

public record AccountResponse(
    Guid Id,
    string Name,
    string? Bank,
    AccountType Type,
    decimal InitialBalance,
    decimal CurrentBalance,
    bool IsActive,
    string? BankCode,
    string? Agency,
    string? AgencyDigit,
    string? AccountNumber,
    string? AccountDigit,
    decimal OverdraftLimit,
    /// <summary>Saldo + limite do cheque especial.</summary>
    decimal AvailableBalance);
