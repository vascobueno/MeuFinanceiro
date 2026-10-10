using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Conta corrente e cartão são origens diferentes (regra 3). Um CreditCard é o GRUPO de cartões que
/// caem na mesma fatura — os cartões em si (titular, adicionais, virtuais) são <see cref="IssuedCard"/>.
/// A bandeira é do grupo: a fatura é uma só, de uma bandeira só.
/// </summary>
public class CreditCard : GrupoOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public CardBrand? Brand { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public decimal? Limit { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<IssuedCard> IssuedCards { get; set; } = new List<IssuedCard>();
    public ICollection<FinancialTransaction> Transactions { get; set; } = new List<FinancialTransaction>();
}
