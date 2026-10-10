namespace MeuFinanceiro.Domain.Enums;

/// <summary>O que é cada linha da fatura (docs/08).</summary>
public enum CardInvoiceItemKind
{
    /// <summary>Compra à vista.</summary>
    Expense = 1,
    /// <summary>Parcela de uma compra parcelada ("04/06").</summary>
    Installment = 2,
    /// <summary>Pagamento da fatura anterior (negativo).</summary>
    Payment = 3,
    /// <summary>Estorno, crédito, cashback (negativo).</summary>
    Credit = 4,
    /// <summary>Anuidade, tarifa, seguro, juros, multa.</summary>
    Fee = 5,
    /// <summary>IOF — ligado à compra no exterior a que se refere.</summary>
    Tax = 6
}
