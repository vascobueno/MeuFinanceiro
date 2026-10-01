using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Exceção ao dia de fechamento do cartão para UM mês. A operadora às vezes antecipa ou adia o
/// fechamento (fim de semana, feriado), e o <see cref="CreditCard.ClosingDay"/> do cadastro vale
/// para todos os meses.
///
/// Só existe linha quando há exceção: sem ela, o fechamento é o calculado (CardCycleCalculator).
///
/// Invariante: <see cref="ClosingDate"/> tem de cair DENTRO de <see cref="ReferenceMonth"/>.
/// Sem isso, um ajuste poderia fazer dois ciclos se sobreporem — e o mesmo lançamento seria
/// contado em duas faturas — ou se inverterem, deixando uma janela vazia. Os dois erram calados.
/// </summary>
public class CardCycleClosing : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Mês de referência da fatura, sempre dia 1.</summary>
    public DateOnly ReferenceMonth { get; set; }

    /// <summary>O fechamento real deste mês.</summary>
    public DateOnly ClosingDate { get; set; }
}
