namespace MeuFinanceiro.Application.Cards;

public record CreateCardPaymentRequest(
    Guid CreditCardId,
    /// <summary>Mês da fatura (dia 1). O ciclo é identificado pelo mês, não pelo fechamento.</summary>
    DateOnly CycleReference,
    DateOnly PaymentDate,
    decimal Amount,
    /// <summary>Null = pago por outra forma (não sai de nenhuma conta).</summary>
    Guid? AccountId
);

public record UpdateCardPaymentRequest(
    DateOnly PaymentDate,
    decimal Amount,
    Guid? AccountId
);

public record CardPaymentResponse(
    Guid Id,
    Guid CreditCardId,
    DateOnly CycleReference,
    DateOnly PaymentDate,
    decimal Amount,
    Guid? AccountId,
    string? AccountName,
    Guid? PaymentTransactionId
);

/// <summary>
/// A fatura de um ciclo, calculada a partir dos lançamentos — não confundir com CardInvoice, que é
/// o documento PDF importado do banco para conciliação (docs/08).
/// </summary>
public record CardInvoiceCycleResponse(
    Guid CreditCardId,
    string CardName,
    /// <summary>Mês de referência (dia 1) — a identidade do ciclo.</summary>
    DateOnly Reference,
    DateOnly Start,
    DateOnly ClosingDate,
    DateOnly DueDate,
    /// <summary>Gastos do ciclo, com sinal: débito soma, crédito (estorno) subtrai.</summary>
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    bool IsClosed,
    bool IsOverdue,
    IReadOnlyList<CardPaymentResponse> Payments
);
