using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

public class CardInvoiceItem : AuditableEntity
{
    public Guid CardInvoiceId { get; set; }
    public CardInvoice? CardInvoice { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? InstallmentLabel { get; set; }
    /// <summary>Valor em reais — o que entra no total da fatura.</summary>
    public decimal Amount { get; set; }

    public CardInvoiceItemKind Kind { get; set; } = CardInvoiceItemKind.Expense;
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }

    /// <summary>Final do cartão em cuja seção da fatura o item veio ("PORTADOR - 5480 XXXX XXXX 1111").</summary>
    public string? CardLastFour { get; set; }
    /// <summary>O cartão cadastrado com esse final, no grupo da fatura — quando há.</summary>
    public Guid? IssuedCardId { get; set; }
    public IssuedCard? IssuedCard { get; set; }

    // Compra no exterior: a fatura traz o valor na moeda da coluna (US$) e a cotação usada.
    public decimal? ForeignAmount { get; set; }
    public string? ForeignCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    /// <summary>A fatura não trouxe a cotação: ela foi calculada (reais ÷ valor estrangeiro).</summary>
    public bool ExchangeRateEstimated { get; set; }

    /// <summary>
    /// Moeda e valor de origem quando a descrição os mostra ("EUR 19,50") e são diferentes da
    /// coluna — a compra em euro que o banco converteu para dólar antes de cobrar em reais.
    /// Informação: o que vale para a conta é o valor em reais.
    /// </summary>
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }

    /// <summary>No IOF: a compra no exterior a que ele se refere.</summary>
    public Guid? RelatedItemId { get; set; }
    public CardInvoiceItem? RelatedItem { get; set; }
}
