namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>docs/09-conciliacao-conta-corrente.md — item normalizado do extrato bancário.</summary>
/// <param name="EffectiveDate">Data da compra no débito, quando o extrato a traz e ela difere da do
/// lançamento. A conciliação compara com ela — é a data em que o usuário registra a compra.</param>
/// <param name="ExternalId">FITID do OFX — o mesmo movimento em outro arquivo não entra de novo.</param>
public record BankStatementItemRequest(DateOnly Date, string Description, string? Document, string? ItemType, decimal Amount,
    DateOnly? EffectiveDate = null, string? ExternalId = null);

public record ImportBankStatementRequest(
    Guid AccountId,
    DateOnly ReferenceStart,
    DateOnly ReferenceEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? OriginalFileName,
    IReadOnlyList<BankStatementItemRequest> Items,
    // Extrato que não fecha só entra com confirmação explícita de quem importa.
    bool ConfirmUnbalanced = false
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
    int ItemCount,
    /// <summary>Movimentos do arquivo que já estavam importados (mesmo FITID) e ficaram de fora.</summary>
    int SkippedDuplicates = 0
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
    string? Reason,
    string ItemType = "OTHER_EXPENSE",
    DateOnly? ItemEffectiveDate = null,
    /// <summary>Aceito pelo usuário — Matched sozinho pode ser só a sugestão da importação.</summary>
    bool Accepted = false
);

public record ManualReconciliationRequest(Guid BankStatementItemId, Guid FinancialTransactionId);
