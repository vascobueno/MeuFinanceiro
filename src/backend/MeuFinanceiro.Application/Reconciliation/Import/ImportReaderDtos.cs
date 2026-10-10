namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>Linha que não pôde ser lida, com o motivo em português e o conteúdo original.</summary>
public record ImportLineError(int Line, string Content, string Message);

/// <summary>Texto colado do extrato, e o período que dá o ano às datas <c>dd/MM</c>.</summary>
public record ParseBankStatementRequest(string Text, DateOnly ReferenceStart, DateOnly ReferenceEnd);

/// <summary>
/// Itens já normalizados, prontos para a importação. Os saldos vêm preenchidos quando o texto
/// trazia as linhas <c>SALDO EM dd/MM</c>/<c>SALDO ANTERIOR</c>: a tela os usa como sugestão.
/// </summary>
public record ParsedBankStatementResponse(
    IReadOnlyList<BankStatementItemRequest> Items,
    IReadOnlyList<ImportLineError> Errors,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    decimal MovementsSum);

/// <summary>Texto colado da fatura, e o fechamento que dá o ano às datas <c>dd/MM</c>.</summary>
public record ParseCardInvoiceRequest(string Text, DateOnly ClosingDate);

public record ParsedCardInvoiceResponse(
    IReadOnlyList<CardInvoiceItemRequest> Items,
    IReadOnlyList<ImportLineError> Errors,
    decimal ItemsSum);
