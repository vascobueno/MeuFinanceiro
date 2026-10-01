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

/// <summary>Ajuste do fechamento de UMA fatura (docs/05 — Pagamento de cartão).</summary>
public record SetCycleClosingRequest(DateOnly ClosingDate);

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
    /// <summary>O fechamento deste mês foi ajustado (difere do dia do cadastro).</summary>
    bool IsClosingOverridden,
    /// <summary>O fechamento que o cadastro do cartão daria — para o "voltar ao padrão".</summary>
    DateOnly DefaultClosingDate,
    IReadOnlyList<CardPaymentResponse> Payments
);
