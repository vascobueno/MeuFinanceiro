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

/// <summary>
/// OFX lido, pronto para a tela conferir e mandar para a importação comum.
/// </summary>
/// <param name="AccountId">A conta cadastrada cujos dados bancários casam com o arquivo — nula se
/// nenhuma ou mais de uma casar.</param>
/// <param name="AccountFromFile">O que o arquivo diz da conta ("banco 033, agência 0001, conta 12345-6").</param>
/// <param name="OpeningBalance">OFX não traz saldo inicial: é o final menos os movimentos.</param>
/// <param name="AlreadyImported">Movimentos com FITID já importado nesta conta — a importação os pula.</param>
public record ParsedOfxResponse(
    Guid? AccountId,
    string? AccountName,
    string AccountFromFile,
    int MatchingAccounts,
    DateOnly? ReferenceStart,
    DateOnly? ReferenceEnd,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    IReadOnlyList<BankStatementItemRequest> Items,
    IReadOnlyList<ImportLineError> Errors,
    decimal MovementsSum,
    int AlreadyImported);
