using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class InvestmentPosition : AuditableEntity
{
    public Guid InvestmentId { get; set; }
    public Investment? Investment { get; set; }

    public DateOnly Date { get; set; }
    public decimal Principal { get; set; }
    public decimal GrossValue { get; set; }
    public decimal TaxWithheld { get; set; }
    public decimal NetValue { get; set; }
}
