using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Installments;

/// <summary>
/// Tela Parcelamento (docs/screens/parcelamento). O mockup só mostra os campos de cálculo
/// (modo/valor/quantidade/primeira parcela); descrição e origem (conta ou cartão) são
/// obrigatórias porque toda parcela vira um FinancialTransaction real (regra 3 e 21) e por
/// isso precisam ser coletadas também, mesmo não aparecendo no mockup.
/// </summary>
public record CreateInstallmentPlanRequest(
    string Description,
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CategoryId,
    InstallmentMode Mode,
    decimal Amount,
    int InstallmentCount,
    DateOnly FirstDueDate
);

public record InstallmentPlanResponse(
    decimal TotalAmount,
    int InstallmentCount,
    IReadOnlyList<decimal> InstallmentAmounts
);

/// <summary>
/// Linha da lista de parcelamentos. O plano em si não guarda conta/cartão — a origem vive nos
/// FinancialTransaction gerados por cada parcela, por isso ela (e o andamento) são resolvidos
/// a partir dos lançamentos vinculados.
/// </summary>
public record InstallmentPlanListItemResponse(
    Guid Id,
    string Description,
    InstallmentMode Mode,
    decimal TotalAmount,
    int InstallmentCount,
    int PaidCount,
    string? AccountName,
    string? CreditCardName,
    DateOnly FirstDueDate,
    DateOnly LastDueDate,
    int? NextInstallmentNumber,
    DateOnly? NextDueDate,
    decimal? NextAmount
);

public record InstallmentPlanDetailResponse(
    Guid Id,
    string Description,
    InstallmentMode Mode,
    decimal TotalAmount,
    int InstallmentCount,
    int PaidCount,
    string? AccountName,
    string? CreditCardName,
    IReadOnlyList<InstallmentItemResponse> Installments
);

public record InstallmentItemResponse(
    Guid Id,
    int Number,
    decimal Amount,
    DateOnly DueDate,
    TransactionStatus? Status,
    Guid? FinancialTransactionId
);
