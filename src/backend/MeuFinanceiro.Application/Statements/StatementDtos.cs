using MeuFinanceiro.Application.Common;

namespace MeuFinanceiro.Application.Statements;

/// <summary>
/// Filtros server-side da tela Extratos (docs/09-conciliacao-conta-corrente.md): conta, período
/// e faixa de saldo final.
///
/// Espelha <see cref="Invoices.CardInvoiceFilter"/>, menos o Status: extrato não tem vencimento,
/// então não há o que estar "aberto" ou "vencido".
/// </summary>
public class BankStatementFilter : PagedRequest
{
    public Guid? AccountId { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

/// <summary>
/// Linha da tela Extratos. <paramref name="NetChange"/> e <paramref name="ItemCount"/> são
/// calculados: o primeiro é saldo final menos inicial, o segundo a contagem de itens do extrato.
/// </summary>
public record BankStatementListItemResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    DateOnly ReferenceStart,
    DateOnly ReferenceEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal NetChange,
    int ItemCount,
    string? OriginalFileName
);
