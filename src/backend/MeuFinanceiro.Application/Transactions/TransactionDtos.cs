using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Transactions;

/// <summary>
/// Requisição de criação de lançamento. Suporta 3 modos, conforme docs/06 e docs/07:
/// 1) Lançamento simples (Installment/Recurring nulos).
/// 2) Parcelamento — via <see cref="Installment"/> (Modo A: TotalAmount / Modo B: InstallmentAmount).
/// 3) Recorrência — via <see cref="Recurring"/>.
/// Transferência entre contas próprias (regra 9) usa Type=Transfer + DestinationAccountId.
/// </summary>
public record CreateTransactionRequest(
    DateOnly TransactionDate,
    string Description,
    Guid? CategoryId,
    Guid? SubcategoryId,
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? DestinationAccountId,
    TransactionType Type,
    TransactionDirection Direction,
    decimal Amount,
    string? Notes,
    InstallmentRequest? Installment,
    RecurringRequest? Recurring,
    ForeignPurchaseRequest? Foreign = null,
    /// <summary>O cartão usado. As parcelas herdam; recorrência e transferência não levam.</summary>
    Guid? IssuedCardId = null
);

/// <summary>
/// Compra no exterior: valor e moeda da cobrança (US$ 21,22), a cotação (5,4970) e, se houver,
/// a moeda de origem (EUR 19,50). O valor em reais continua em Amount.
/// </summary>
public record ForeignPurchaseRequest(decimal Amount, string Currency, decimal? ExchangeRate = null,
    decimal? OriginalAmount = null, string? OriginalCurrency = null);

public record ForeignPurchaseResponse(decimal Amount, string Currency, decimal? ExchangeRate,
    decimal? OriginalAmount, string? OriginalCurrency);

public record InstallmentRequest(InstallmentMode Mode, decimal Amount, int InstallmentCount, DateOnly FirstDueDate);

public record RecurringRequest(RecurrenceFrequency Frequency, DateOnly? EndDate);

public record UpdateTransactionRequest(
    DateOnly TransactionDate,
    string Description,
    Guid? CategoryId,
    Guid? SubcategoryId,
    Guid? AccountId,
    Guid? CreditCardId,
    decimal Amount,
    TransactionDirection Direction,
    TransactionStatus Status,
    string? Notes,
    // Nulo tira os dados de compra no exterior; o cliente manda o que quer manter.
    ForeignPurchaseRequest? Foreign = null,
    Guid? IssuedCardId = null
);

public record TransactionResponse(
    Guid Id,
    DateOnly TransactionDate,
    string Description,
    Guid? MerchantId,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SubcategoryId,
    Guid? AccountId,
    string? AccountName,
    Guid? CreditCardId,
    string? CreditCardName,
    TransactionType Type,
    TransactionDirection Direction,
    decimal Amount,
    TransactionStatus Status,
    Guid? InstallmentId,
    int? InstallmentNumber,
    int? InstallmentCount,
    /// <summary>Plano a que a parcela pertence — é nele que fica o anexo do parcelamento,
    /// e é o que a tela precisa depois de criar um lançamento parcelado.</summary>
    Guid? InstallmentPlanId,
    Guid? RecurringTransactionId,
    string? Notes,
    /// <summary>Quantos anexos — só para a lista mostrar o clipe sem abrir cada lançamento.
    /// Numa parcela, inclui os anexos do plano.</summary>
    int AttachmentCount = 0,
    ForeignPurchaseResponse? Foreign = null,
    Guid? IssuedCardId = null,
    string? IssuedCardLastFour = null,
    IssuedCardKind? IssuedCardKind = null
);

/// <summary>Filtros server-side de docs/06-lancamentos.md (período, descrição, categoria, conta, cartão, tipo, status, valor).</summary>
public class TransactionFilter : Common.PagedRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CreditCardId { get; set; }
    /// <summary>Conta OU cartão, sem precisar dizer qual — as duas telas de Lançamentos.</summary>
    public TransactionOrigin? Origin { get; set; }
    public TransactionType? Type { get; set; }
    public TransactionStatus? Status { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}
