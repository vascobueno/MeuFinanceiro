using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Conta corrente e cartão são origens diferentes (regra 3).</summary>
public class CreditCard : HouseholdOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public decimal? Limit { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
