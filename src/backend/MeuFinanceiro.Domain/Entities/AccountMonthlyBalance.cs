using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Saldo de fechamento de uma conta ao fim de um mês. É um valor derivado (cache), nunca a
/// verdade: a verdade continua sendo a soma dos lançamentos. Serve para (a) não varrer o
/// histórico inteiro a cada leitura e (b) permitir arquivar lançamentos antigos sem que o saldo
/// fique errado — hoje apagá-los deslocaria o saldo em silêncio.
/// </summary>
public class AccountMonthlyBalance : GrupoOwnedEntity
{
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>Último dia do mês fechado.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Soma acumulada dos lançamentos até PeriodEnd. NÃO inclui o InitialBalance da conta de
    /// propósito: o saldo inicial é editável (AccountService.UpdateAsync), e embuti-lo aqui
    /// faria toda linha virar lixo a cada edição.
    /// </summary>
    public decimal CumulativeAmount { get; set; }

    /// <summary>Movimento do próprio mês — existe para conferência (Σ MonthAmount == CumulativeAmount).</summary>
    public decimal MonthAmount { get; set; }

    /// <summary>
    /// Marca a linha de corte de um arquivamento. Uma âncora é imutável: a regeneração parte
    /// dela em vez do zero, e a invalidação nunca a apaga. Sem isso, arquivar e depois editar
    /// algo antigo recomporia o saldo a partir de um histórico incompleto — errado e silencioso.
    /// </summary>
    public bool IsAnchor { get; set; }
}
