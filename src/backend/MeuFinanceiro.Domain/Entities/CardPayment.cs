using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Pagamento de uma fatura de cartão (docs/05 — Pagamento de cartão, docs/07).
///
/// Entidade própria, e não apenas um par de lançamentos como a transferência, por dois motivos:
/// pagar "por outra forma" não tem perna em conta nenhuma (<see cref="AccountId"/> nulo), e o
/// pagamento é justamente a coisa que o usuário edita depois — trocar a conta de onde saiu.
///
/// O ciclo que ele quita é identificado pelo MÊS (<see cref="CycleReference"/>), não pela data de
/// fechamento. A data pode ser ajustada por fatura (ver CardCycleClosing) quando a operadora
/// antecipa ou adia o fechamento — se ela fosse a chave, um ajuste deixaria os pagamentos já
/// registrados órfãos e eles sumiriam da tela sem aviso. O mês não muda.
/// </summary>
public class CardPayment : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Qual fatura este pagamento quita — o mês de referência, sempre dia 1.</summary>
    public DateOnly CycleReference { get; set; }

    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Conta de onde saiu o dinheiro. NULL significa "pago por outra forma".</summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    /// <summary>
    /// Lançamento gerado na conta, que é o que derruba o saldo. NULL quando pago por outra forma.
    /// </summary>
    public Guid? PaymentTransactionId { get; set; }
    public FinancialTransaction? PaymentTransaction { get; set; }
}
