using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Exceções ao calendário do cartão para UM mês. A operadora às vezes antecipa ou adia o
/// fechamento (fim de semana, feriado), e o vencimento pode ser remarcado — e os dias do cadastro
/// (<see cref="CreditCard.ClosingDay"/> / <see cref="CreditCard.DueDay"/>) valem para todos os
/// meses.
///
/// Uma linha por (cartão, mês), com os dois campos anuláveis, em vez de duas tabelas: é a mesma
/// chave, resolve numa consulta só, e limpar tudo é um DELETE. Linha com os dois nulos não deve
/// existir — ao limpar um ajuste com o outro já nulo, a linha é removida.
///
/// Invariante do fechamento: <see cref="ClosingDate"/> tem de cair DENTRO de
/// <see cref="ReferenceMonth"/>. Sem isso, dois ciclos poderiam se sobrepor — o mesmo lançamento
/// entraria em duas faturas — ou se inverter, deixando uma janela vazia. Os dois erram em silêncio.
///
/// O vencimento não tem invariante rígido porque não define a janela do ciclo: ele só aparece na
/// tela e no cálculo de atraso. A única regra é não vencer antes de fechar.
/// </summary>
public class CardCycleOverride : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Mês de referência da fatura, sempre dia 1.</summary>
    public DateOnly ReferenceMonth { get; set; }

    /// <summary>Fechamento ajustado; null = usa o dia do cadastro.</summary>
    public DateOnly? ClosingDate { get; set; }

    /// <summary>Vencimento ajustado; null = usa o derivado do fechamento padrão.</summary>
    public DateOnly? DueDate { get; set; }
}
