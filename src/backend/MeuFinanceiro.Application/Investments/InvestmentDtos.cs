using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Investments;

public record CreateInvestmentAccountRequest(string Institution, string? Name);
public record InvestmentAccountResponse(Guid Id, string Institution, string? Name, decimal TotalPosition);

public record CreateInvestmentRequest(Guid InvestmentAccountId, string Name, string? Type, DateOnly? MaturityDate, string? Liquidity);

public record InvestmentResponse(
    Guid Id,
    Guid InvestmentAccountId,
    string Name,
    string? Type,
    DateOnly? MaturityDate,
    string? Liquidity,
    decimal Principal,
    decimal NetValue
);

/// <summary>
/// Regras 10/11 (docs/05-regras-negocio.md): aplicação reduz conta e aumenta investimento;
/// resgate faz o inverso; rendimento é evento separado que não passa pela conta.
/// AccountId é obrigatório para Application/Redemption (gera o lançamento espelho na conta)
/// e ignorado para Yield.
/// </summary>
public record CreateInvestmentTransactionRequest(Guid InvestmentId, InvestmentTransactionType Type, DateOnly Date, decimal Amount, Guid? AccountId);

public record InvestmentTransactionResponse(Guid Id, Guid InvestmentId, InvestmentTransactionType Type, DateOnly Date, decimal Amount, Guid? FinancialTransactionId);

/// <summary>Dashboard: saldo em contas x saldo em investimentos, nunca somados individualmente (regra 7).</summary>
public record InvestmentsSummaryResponse(decimal TotalPosition, IReadOnlyList<InvestmentAccountResponse> Accounts);
