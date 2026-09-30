using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Pagamento de uma fatura de cartão (docs/05 — Pagamento de cartão, docs/07).
///
/// Entidade própria, e não apenas um par de lançamentos como a transferência, por dois motivos:
/// pagar "por outra forma" não tem perna em conta nenhuma (<see cref="AccountId"/> nulo), e o
/// pagamento é justamente a coisa que o usuário edita depois — trocar a conta de onde saiu.
///
/// O ciclo que ele quita é identificado por <see cref="CycleClosingDate"/>, que é derivada do dia
/// de fechamento do cartão (ver CardCycleCalculator) e não de uma tabela de faturas: o ciclo é
/// calculado, não persistido.
/// </summary>
public class CardPayment : GrupoOwnedEntity
{
    public Guid CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    /// <summary>Qual fatura este pagamento quita — a data de fechamento do ciclo.</summary>
    public DateOnly CycleClosingDate { get; set; }

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
