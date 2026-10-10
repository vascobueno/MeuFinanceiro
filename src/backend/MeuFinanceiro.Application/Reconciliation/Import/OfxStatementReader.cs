using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>O que um arquivo OFX diz sobre o extrato, antes de saber de qual conta é.</summary>
public record OfxStatement(
    string? BankId,
    string? BranchId,
    string? AccountId,
    DateOnly? Start,
    DateOnly? End,
    decimal? LedgerBalance,
    IReadOnlyList<BankStatementItemRequest> Items,
    IReadOnlyList<ImportLineError> Errors);

/// <summary>
/// Lê extrato bancário em OFX (docs/09). Aceita OFX 1.x — SGML, em que as tags de valor não fecham
/// (<c>&lt;TRNAMT&gt;-18.00</c> seguido da próxima tag) — e OFX 2.x, que é XML.
///
/// O que se aproveita: BANKID/BRANCHID/ACCTID para reconhecer a conta; DTSTART/DTEND para o
/// período; LEDGERBAL para o saldo final; de cada STMTTRN a data (DTPOSTED), o valor (TRNAMT), a
/// descrição (MEMO e/ou NAME), o documento (CHECKNUM ou REFNUM) e o FITID, o identificador que o
/// banco dá ao movimento e que não muda entre downloads — é ele que permite baixar períodos que se
/// sobrepõem sem duplicar movimento.
///
/// OFX não traz saldo inicial: ele é calculado (saldo final − movimentos) por quem chama.
/// </summary>
public static partial class OfxStatementReader
{
    private const int MaxPurchaseLagDays = 10;

    /// <summary>
    /// Decodifica os bytes do arquivo. OFX 1.x de banco brasileiro costuma vir em Windows-1252
    /// (<c>CHARSET:1252</c> no cabeçalho); Latin-1 coincide com ele em todas as letras acentuadas
    /// do português e não exige registrar provedor de codepage.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 400)).ToUpperInvariant();
        var declaresUtf8 = head.Contains("UTF-8") || head.Contains("CHARSET:UTF");
        if (declaresUtf8) return new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static OfxStatement Read(string? content)
    {
        var text = content ?? string.Empty;
        var errors = new List<ImportLineError>();

        if (!text.Contains("<OFX>", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ImportLineError(1, Shorten(text), "Não parece um arquivo OFX: falta a tag <OFX>."));
            return new OfxStatement(null, null, null, null, null, null, [], errors);
        }

        var accountBlock = Block(text, "BANKACCTFROM") ?? text;
        var bankId = Tag(accountBlock, "BANKID");
        var branchId = Tag(accountBlock, "BRANCHID");
        var acctId = Tag(accountBlock, "ACCTID");

        var tranList = Block(text, "BANKTRANLIST") ?? text;
        var start = ParseDate(Tag(tranList, "DTSTART"));
        var end = ParseDate(Tag(tranList, "DTEND"));

        decimal? ledger = null;
        var ledgerBlock = Block(text, "LEDGERBAL");
        if (ledgerBlock is not null && TryAmount(Tag(ledgerBlock, "BALAMT"), out var balance)) ledger = balance;

        var items = new List<BankStatementItemRequest>();
        var index = 0;
        foreach (Match m in Transaction().Matches(text))
        {
            index++;
            var block = m.Groups[1].Value;
            var fitId = Tag(block, "FITID");
            var posted = ParseDate(Tag(block, "DTPOSTED"));
            var amountRaw = Tag(block, "TRNAMT");
            var description = Description(Tag(block, "NAME"), Tag(block, "MEMO"));

            if (posted is null)
            {
                errors.Add(new ImportLineError(index, Shorten(block), "Movimento sem data (DTPOSTED)."));
                continue;
            }
            if (!TryAmount(amountRaw, out var amount))
            {
                errors.Add(new ImportLineError(index, Shorten(block), $"Valor inválido: \"{amountRaw}\"."));
                continue;
            }
            if (description.Length == 0)
            {
                errors.Add(new ImportLineError(index, Shorten(block), "Movimento sem descrição (MEMO/NAME)."));
                continue;
            }

            var type = StatementItemClassifier.Classify(description, amount);
            DateOnly? effective = null;
            if (type == StatementItemClassifier.DebitCardPurchase)
                (description, effective) = PurchaseDate(description, posted.Value);

            var document = Tag(block, "CHECKNUM") ?? Tag(block, "REFNUM");
            items.Add(new BankStatementItemRequest(posted.Value, description, document, type, amount, effective,
                string.IsNullOrWhiteSpace(fitId) ? null : fitId));
        }

        if (items.Count == 0 && errors.Count == 0)
            errors.Add(new ImportLineError(1, "", "O arquivo não tem movimentos (STMTTRN)."));

        return new OfxStatement(bankId, branchId, acctId, start, end, ledger, items, errors);
    }

    /// <summary>
    /// A compra no débito traz no MEMO a data em que foi feita ("COMPRA CARTAO DEB MC 02/01 LOJA"),
    /// como na linha do estabelecimento do extrato colado (StatementTextReader).
    /// </summary>
    private static (string description, DateOnly? effective) PurchaseDate(string description, DateOnly posted)
    {
        var m = DayMonthToken().Match(description);
        if (!m.Success || !ImportValueParser.TryParseDate(m.Value, posted, out var date)) return (description, null);
        if (date > posted || posted.DayNumber - date.DayNumber > MaxPurchaseLagDays) return (description, null);

        var cleaned = Spaces().Replace(description.Remove(m.Index, m.Length), " ").Trim();
        return (cleaned, date < posted ? date : null);
    }

    /// <summary>NAME e MEMO juntos quando os dois trazem algo diferente; senão o que houver.</summary>
    private static string Description(string? name, string? memo)
    {
        var n = name?.Trim() ?? "";
        var m = memo?.Trim() ?? "";
        if (n.Length == 0) return m;
        if (m.Length == 0 || m.Contains(n, StringComparison.OrdinalIgnoreCase)) return m.Length == 0 ? n : m;
        if (n.Contains(m, StringComparison.OrdinalIgnoreCase)) return n;
        return $"{n} {m}";
    }

    /// <summary>
    /// Valor de uma tag: até o próximo "&lt;" (SGML, sem fechamento) ou até o fechamento (XML).
    /// </summary>
    private static string? Tag(string block, string tag)
    {
        var m = Regex.Match(block, $@"<{tag}>([^<\r\n]*)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var value = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? Block(string text, string tag)
    {
        var m = Regex.Match(text, $@"<{tag}>(.*?)</{tag}>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>"20230102120000[-3:BRT]" → 02/01/2023. Só a data interessa.</summary>
    private static DateOnly? ParseDate(string? raw)
    {
        if (raw is null || raw.Length < 8) return null;
        return DateOnly.TryParseExact(raw[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>TRNAMT vem com ponto ("-18.00") ou, em alguns bancos brasileiros, com vírgula ("-18,00").</summary>
    private static bool TryAmount(string? raw, out decimal value) => ImportValueParser.TryParseMoney(raw, out value);

    private static string Shorten(string value)
    {
        var flat = Spaces().Replace(value, " ").Trim();
        return flat.Length <= 120 ? flat : flat[..120] + "…";
    }

    // STMTTRN fecha em OFX 1.x e 2.x; o "?" deixa cada movimento num match.
    [GeneratedRegex(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Transaction();

    [GeneratedRegex(@"(?<!\d)\d{2}/\d{2}(?![/\d])")]
    private static partial Regex DayMonthToken();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
