namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>docs/08-conciliacao-cartoes.md — item normalizado da fatura de cartão.</summary>
public record CardInvoiceItemRequest(DateOnly Date, string Description, string? InstallmentLabel, decimal Amount);

public record ImportCardInvoiceRequest(
    Guid CreditCardId,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    string? OriginalFileName,
    IReadOnlyList<CardInvoiceItemRequest> Items,
    // Saldo da fatura anterior: a fatura fecha como saldo anterior + itens = total.
    decimal PreviousBalance = 0,
    bool ConfirmUnbalanced = false
);

public record CardInvoiceResponse(
    Guid Id,
    Guid CreditCardId,
    string CreditCardName,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    bool TotalValidated,
    int ItemCount
);

public record CardReconciliationResponse(
    Guid Id,
    Guid CardInvoiceItemId,
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

public record ManualCardReconciliationRequest(Guid CardInvoiceItemId, Guid FinancialTransactionId);
