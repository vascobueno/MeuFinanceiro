using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/08-conciliacao-cartoes.md — fatura importada (entrada inicial: PDF).</summary>
public class CardInvoice : HouseholdOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }
    public DateOnly ClosingDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileHash { get; set; }

    public ICollection<CardInvoiceItem> Items { get; set; } = new List<CardInvoiceItem>();
}
