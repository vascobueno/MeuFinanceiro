using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Cada parcela é independente para efeitos de conciliação (docs/07).</summary>
public class Installment : AuditableEntity
{
    public Guid InstallmentPlanId { get; set; }
    public InstallmentPlan? InstallmentPlan { get; set; }

    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }

    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }
}
