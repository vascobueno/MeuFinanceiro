using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Ex.: 01/01/2026 -> R$150, 01/09/2026 -> R$175 (docs/07-recorrencias-e-parcelamentos.md).</summary>
public class RecurringTransactionVersion : AuditableEntity
{
    public Guid RecurringTransactionId { get; set; }
    public RecurringTransaction? RecurringTransaction { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public decimal Amount { get; set; }
}
