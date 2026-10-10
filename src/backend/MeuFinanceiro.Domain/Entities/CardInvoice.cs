using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/08-conciliacao-cartoes.md — fatura importada (entrada inicial: PDF).</summary>
public class CardInvoice : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }
    public DateOnly ClosingDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Saldo da fatura anterior, como impresso na fatura. A conta que a fatura fecha é
    /// saldo anterior + itens (pagamento entra negativo) = total.
    /// </summary>
    public decimal PreviousBalance { get; set; }

    public string? OriginalFileName { get; set; }
    public string? FileHash { get; set; }

    /// <summary>Impressão digital do conteúdo — ver BankStatement.ContentHash.</summary>
    public string? ContentHash { get; set; }

    /// <summary>Saldo anterior + soma dos itens bateu com o total na importação.</summary>
    public bool BalanceValidated { get; set; }

    public ICollection<CardInvoiceItem> Items { get; set; } = new List<CardInvoiceItem>();
}
