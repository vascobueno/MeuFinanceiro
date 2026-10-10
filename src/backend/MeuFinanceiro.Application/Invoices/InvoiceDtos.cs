using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Invoices;

/// <summary>Filtros server-side da tela Faturas (docs/screens/faturas): cartão, período, status, valor.</summary>
public class CardInvoiceFilter : PagedRequest
{
    public Guid? CreditCardId { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string? Status { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Status apresentado na grid: derivado da data de vencimento, já que a fatura não tem um
/// campo de status próprio no modelo de dados (docs/08-conciliacao-cartoes.md).
/// </summary>
public enum CardInvoiceStatus
{
    Aberta,
    Vencida
}

public record CardInvoiceListItemResponse(
    Guid Id,
    Guid CreditCardId,
    string CreditCardName,
    DateOnly ClosingDate,
    DateOnly DueDate,
    decimal TotalAmount,
    CardInvoiceStatus Status,
    bool BalanceValidated
);
