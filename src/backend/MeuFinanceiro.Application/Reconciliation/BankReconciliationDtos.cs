namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>docs/09-conciliacao-conta-corrente.md — item normalizado do extrato bancário.</summary>
public record BankStatementItemRequest(DateOnly Date, string Description, string? Document, string ItemType, decimal Amount);

public record ImportBankStatementRequest(
    Guid AccountId,
    DateOnly ReferenceStart,
    DateOnly ReferenceEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? OriginalFileName,
    IReadOnlyList<BankStatementItemRequest> Items
);

public record BankStatementResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    DateOnly ReferenceStart,
    DateOnly ReferenceEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    bool BalanceValidated,
    int ItemCount
);

public record BankReconciliationResponse(
    Guid Id,
    Guid BankStatementItemId,
    DateOnly ItemDate,
    string ItemDescription,
    decimal ItemAmount,
    Guid? FinancialTransactionId,
    string Status,
    int Score,
    decimal AmountDifference,
    int DateDifferenceDays,
    string? Reason
);

public record ManualReconciliationRequest(Guid BankStatementItemId, Guid FinancialTransactionId);
