using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// docs/08-conciliacao-cartoes.md — score: valor 35, data 25, descrição 25, parcela/documento 10, tipo 5.
/// 90–100 MATCHED; 70–89 POSSIBLE_MATCH; abaixo de 70 UNMATCHED.
/// </summary>
public class Reconciliation : HouseholdOwnedEntity
{
    public ReconciliationSourceType SourceType { get; set; }

    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }

    public Guid? BankStatementItemId { get; set; }
    public BankStatementItem? BankStatementItem { get; set; }

    public Guid? CardInvoiceItemId { get; set; }
    public CardInvoiceItem? CardInvoiceItem { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Unmatched;
    public int Score { get; set; }
    public decimal AmountDifference { get; set; }
    public int DateDifferenceDays { get; set; }
    public int DescriptionScore { get; set; }
    public string? Reason { get; set; }
}
