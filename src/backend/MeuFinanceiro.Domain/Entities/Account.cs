using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Conta corrente/poupança. Saldo não inclui investimentos (regra 7).</summary>
public class Account : GrupoOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public AccountType Type { get; set; } = AccountType.Checking;
    public decimal InitialBalance { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
