using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Regra 13: alterações de recorrência criam nova versão por data de vigência.
/// Ocorrências históricas/conciliadas nunca são alteradas retroativamente.
/// </summary>
public class RecurringTransaction : GrupoOwnedEntity
{
    public string Description { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public Guid? CreditCardId { get; set; }
    public Guid? CategoryId { get; set; }
    public TransactionType Type { get; set; }
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RecurringTransactionVersion> Versions { get; set; } = new List<RecurringTransactionVersion>();
}
