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
    InvestmentRedemption = 5
}
