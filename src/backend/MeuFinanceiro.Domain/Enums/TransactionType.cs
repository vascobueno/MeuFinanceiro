namespace MeuFinanceiro.Domain.Enums;

/// <summary>
/// Regras 09/10/11 de negócio: transferências, aplicações e resgates não são
/// receita/despesa operacional (docs/05-regras-negocio.md).
/// </summary>
public enum TransactionType
{
    Income = 1,
    Expense = 2,
    Transfer = 3,
    InvestmentApplication = 4,
    InvestmentRedemption = 5,

    /// <summary>
    /// Pagamento de fatura de cartão. Tipo próprio, e não Expense, porque a compra no cartão JÁ é
    /// a despesa — lançar o pagamento como despesa contaria o mesmo dinheiro duas vezes. É
    /// movimentação, pela mesma razão da regra 09 (transferência entre contas próprias).
    /// As agregações de despesa filtram Type == Expense e passam a excluí-lo de graça.
    /// </summary>
    CardPayment = 6
}
