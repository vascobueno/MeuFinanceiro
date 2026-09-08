using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Regras 15/16/17: aceita valor total OU valor da parcela + quantidade; a soma das
/// parcelas deve fechar exatamente o principal; arredondamento fica na última parcela.
/// </summary>
public class InstallmentPlan : GrupoOwnedEntity
{
    public string Description { get; set; } = string.Empty;
    public InstallmentMode Mode { get; set; }
    public decimal TotalAmount { get; set; }
    public int InstallmentCount { get; set; }

    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}
