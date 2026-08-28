using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class CardInvoiceItem : AuditableEntity
{
    public Guid CardInvoiceId { get; set; }
    public CardInvoice? CardInvoice { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? InstallmentLabel { get; set; }
    public decimal Amount { get; set; }
}
