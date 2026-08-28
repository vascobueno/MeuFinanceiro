using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Saldo separado das contas correntes (regra 6/7, docs/10-investimentos.md).</summary>
public class InvestmentAccount : HouseholdOwnedEntity
{
    public string Institution { get; set; } = string.Empty;
    public string? Name { get; set; }

    public ICollection<Investment> Investments { get; set; } = new List<Investment>();
}
