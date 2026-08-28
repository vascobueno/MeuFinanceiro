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
