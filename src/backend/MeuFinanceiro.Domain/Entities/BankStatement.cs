using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>docs/09-conciliacao-conta-corrente.md — extrato bancário importado (fonte externa).</summary>
public class BankStatement : HouseholdOwnedEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly ReferenceStart { get; set; }
    public DateOnly ReferenceEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileHash { get; set; }

    public ICollection<BankStatementItem> Items { get; set; } = new List<BankStatementItem>();
}
