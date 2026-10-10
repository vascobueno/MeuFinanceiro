using MeuFinanceiro.Domain.Common;

namespace MeuFinanceiro.Domain.Entities;

public class BankStatementItem : AuditableEntity
{
    public Guid BankStatementId { get; set; }
    public BankStatement? BankStatement { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Document { get; set; }
    /// <summary>Um de StatementItemClassifier.KnownTypes (docs/09): DEBIT_CARD_PURCHASE,
    /// ATM_WITHDRAWAL, PIX_SENT, PIX_RECEIVED, TED_SENT, TED_RECEIVED, BOLETO_PAYMENT, BILL_PAYMENT,
    /// TAX_PAYMENT, DIRECT_DEBIT, BANK_FEE, INTEREST_CHARGE, INVESTMENT, INVESTMENT_REDEMPTION,
    /// INVESTMENT_INTEREST, TRANSFER, OTHER_INCOME, OTHER_EXPENSE.</summary>
    public string ItemType { get; set; } = "OTHER_EXPENSE";
    public decimal Amount { get; set; }
    public decimal? RunningBalance { get; set; }

    /// <summary>
    /// Data da compra no débito, quando o extrato a imprime na linha do estabelecimento
    /// ("02/01 LOJA") e ela é anterior à do lançamento. Nula quando é igual ou não veio.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }
}
