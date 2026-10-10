using MeuFinanceiro.Domain.Common;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Domain.Entities;

/// <summary>
/// docs/08-conciliacao-cartoes.md — score: valor 35, data 25, descrição 25, parcela/documento 10, tipo 5.
/// 90–100 MATCHED; 70–89 POSSIBLE_MATCH; abaixo de 70 UNMATCHED.
/// </summary>
public class Reconciliation : GrupoOwnedEntity
{
    public ReconciliationSourceType SourceType { get; set; }

    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }

    public Guid? BankStatementItemId { get; set; }
    public BankStatementItem? BankStatementItem { get; set; }

    public Guid? CardInvoiceItemId { get; set; }
    public CardInvoiceItem? CardInvoiceItem { get; set; }

    /// <summary>
    /// Pagamento de fatura que corresponde à linha "PAGAMENTO" de uma fatura importada. Alternativa
    /// a <see cref="FinancialTransactionId"/>: o pagamento não é um gasto do cartão, e o lançamento
    /// que ele gera fica na conta, onde é conciliado com o extrato — não pode ser marcado daqui.
    /// </summary>
    public Guid? CardPaymentId { get; set; }
    public CardPayment? CardPayment { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Unmatched;
    public int Score { get; set; }
    public decimal AmountDifference { get; set; }
    public int DateDifferenceDays { get; set; }
    public int DescriptionScore { get; set; }
    public string? Reason { get; set; }

    /// <summary>
    /// Quando o usuário aceitou (ou vinculou à mão). Status Matched sozinho não diz isso: a
    /// importação já grava Matched quando o score passa de 90, e isso é só sugestão. Para o
    /// lançamento o aceite também fica no Status Reconciled dele; para o pagamento de fatura,
    /// que não tem esse status, este campo é o único registro.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }
}
