using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>Regras 10/11: aplicação reduz conta/aumenta investimento; resgate o inverso;
/// rendimento é evento separado (docs/05-regras-negocio.md).</summary>
public class InvestmentTransaction : AuditableEntity
{
    public Guid InvestmentId { get; set; }
    public Investment? Investment { get; set; }

    public InvestmentTransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Lançamento correspondente na conta corrente, quando aplicável.</summary>
    public Guid? FinancialTransactionId { get; set; }
}
