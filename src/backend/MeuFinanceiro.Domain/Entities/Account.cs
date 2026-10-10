using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Conta corrente/poupança. Saldo não inclui investimentos (regra 7).</summary>
public class Account : GrupoOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Bank { get; set; }

    // Dados bancários: opcionais, mas todos juntos (BankDetails.Parse). Só dígitos, sem pontuação;
    // código do banco com 3 e agência com 4 posições.
    public string? BankCode { get; set; }
    public string? Agency { get; set; }
    public string? AgencyDigit { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountDigit { get; set; }

    /// <summary>
    /// Limite do cheque especial. Não entra no saldo nem no "Saldo total": é dinheiro do banco, não
    /// da conta. Aparece só como "Disponível" (saldo + limite).
    /// </summary>
    public decimal OverdraftLimit { get; set; }
    public AccountType Type { get; set; } = AccountType.Checking;
    public decimal InitialBalance { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
