using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// Lançamento financeiro. Regra 20: valores sempre decimal. Regra 12: lançamentos
/// conciliados não são alterados automaticamente. Ver docs/04-modelo-dados.md e docs/06-lancamentos.md.
/// </summary>
public class FinancialTransaction : GrupoOwnedEntity
{
    public DateOnly TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;

    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid? SubcategoryId { get; set; }
    public Subcategory? Subcategory { get; set; }

    /// <summary>Origem: exatamente uma entre AccountId / CreditCardId deve estar preenchida
    /// para lançamentos que não são apenas planejamento (validado na Application).</summary>
    public Guid? AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid? CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }

    public TransactionType Type { get; set; }
    public TransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public Guid? InstallmentId { get; set; }
    public Installment? Installment { get; set; }

    public Guid? RecurringTransactionId { get; set; }
    public RecurringTransaction? RecurringTransaction { get; set; }

    /// <summary>Para transferências entre contas próprias (regra 9): aponta o outro lado do par.</summary>
    public Guid? RelatedTransactionId { get; set; }

    public string? Notes { get; set; }

    // Compra no exterior (docs/06): o valor na moeda da cobrança, a moeda e a cotação usada.
    // Amount continua sendo o valor em reais — é ele que entra em saldo, fatura e relatórios.
    public decimal? ForeignAmount { get; set; }
    public string? ForeignCurrency { get; set; }
    public decimal? ExchangeRate { get; set; }
    /// <summary>Moeda e valor de origem, quando diferentes da moeda da cobrança ("EUR 19,50" cobrado em dólar).</summary>
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }
}
