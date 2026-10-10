namespace MeuFinanceiro.Application.Reconciliation;

/// <summary>docs/08-conciliacao-cartoes.md — item normalizado da fatura de cartão.</summary>
/// <param name="CardLastFour">Final do cartão em cuja seção da fatura o item veio.</param>
/// <param name="Kind">Nulo: classificado na importação (InvoiceTextReader.Classify).</param>
/// <param name="ForeignAmount">Valor na moeda da coluna estrangeira (US$), na compra no exterior.</param>
/// <param name="ExchangeRate">Cotação da linha COTAÇÃO da fatura; ou calculada, com <paramref name="ExchangeRateEstimated"/>.</param>
/// <param name="OriginalAmount">Valor na moeda de origem mostrada na descrição ("EUR 19,50").</param>
/// <param name="RelatedIndex">No IOF: a posição, nesta mesma lista, da compra no exterior a que ele se refere.</param>
public record CardInvoiceItemRequest(
    DateOnly Date,
    string Description,
    string? InstallmentLabel,
    decimal Amount,
    string? CardLastFour = null,
    MeuFinanceiro.Domain.Enums.CardInvoiceItemKind? Kind = null,
    decimal? ForeignAmount = null,
    string? ForeignCurrency = null,
    decimal? ExchangeRate = null,
    bool ExchangeRateEstimated = false,
    decimal? OriginalAmount = null,
    string? OriginalCurrency = null,
    int? RelatedIndex = null,
    int? InstallmentNumber = null,
    int? InstallmentTotal = null);

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
    string? Reason,
    Guid? CardPaymentId = null,
    bool Accepted = false,
    string? ItemCardLastFour = null,
    string? ItemKind = null,
    decimal? ItemForeignAmount = null,
    string? ItemForeignCurrency = null,
    decimal? ItemExchangeRate = null,
    bool ItemExchangeRateEstimated = false,
    decimal? ItemOriginalAmount = null,
    string? ItemOriginalCurrency = null,
    Guid? ItemIssuedCardId = null,
    Guid? ItemRelatedItemId = null
);

/// <summary>Vincula o item a um lançamento OU a um pagamento de fatura — exatamente um dos dois.</summary>
public record ManualCardReconciliationRequest(Guid CardInvoiceItemId, Guid? FinancialTransactionId, Guid? CardPaymentId = null);
