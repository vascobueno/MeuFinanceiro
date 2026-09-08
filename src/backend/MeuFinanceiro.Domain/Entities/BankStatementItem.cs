using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class BankStatementItem : AuditableEntity
{
    public Guid BankStatementId { get; set; }
    public BankStatement? BankStatement { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Document { get; set; }
    /// <summary>DEBIT_CARD_PURCHASE, ATM_WITHDRAWAL, PIX_SENT, PIX_RECEIVED, TED_RECEIVED,
    /// BOLETO_PAYMENT, TAX_PAYMENT, DIRECT_DEBIT, INVESTMENT, INVESTMENT_INTEREST, TRANSFER,
    /// OTHER_INCOME, OTHER_EXPENSE (docs/09).</summary>
    public string ItemType { get; set; } = "OTHER_EXPENSE";
    public decimal Amount { get; set; }
    public decimal? RunningBalance { get; set; }
}
