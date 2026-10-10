using System.Globalization;
using System.Text.RegularExpressions;
using MeuFinanceiro.Domain.Enums;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Lê itens colados de uma fatura de cartão (docs/08-conciliacao-cartoes.md).
///
/// Formatos tirados de faturas reais de dois emissores:
/// <list type="bullet">
/// <item><c>[1-3] dd/MM DESCRIÇÃO [NN/NN] valor [US$]</c> — o dígito antes da data é aceito e
/// ignorado; o segundo valor, quando existe, é o da moeda estrangeira (não é lido como reais);</item>
/// <item><c>dd/MM Descrição [Parcela NN/NN] [-]R$ valor</c>;</item>
/// <item>parcela grudada no fim da descrição: <c>Anuidade Diferenc.titular05-12</c>;</item>
/// <item><c>IOF DESPESA NO EXTERIOR valor</c>, sem data — herda a da compra anterior e fica ligado
/// a ela.</item>
/// </list>
///
/// A fatura vem em seções: um cabeçalho por cartão (<c>PORTADOR - 5480 XXXX XXXX 1111</c>, com
/// "@" no virtual) e, dentro dele, pagamentos, parcelamentos e despesas. Cada item leva o final do
/// cartão da seção em que está. A linha <c>COTAÇÃO DOLAR R$ 5,4970</c> dá a cotação das compras
/// no exterior da seção; sem ela, a cotação é calculada e marcada como tal. <c>VALOR TOTAL</c> dá
/// o total. Outras linhas sem data (resumo, limite, cabeçalhos de coluna) são ignoradas.
/// </summary>
public static partial class InvoiceTextReader
{
    private const string DefaultForeignCurrency = "USD";

    // Moedas que aparecem na descrição de compras no exterior ("EUR 19,50").
    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "ARS", "CLP", "UYU", "PYG", "MXN", "CAD", "AUD", "JPY", "CHF", "CNY", "COP", "PEN", "BOB",
    };

    private sealed class State
    {
        public List<CardInvoiceItemRequest> Items { get; } = new();
        public List<InvoiceCardHeader> Cards { get; } = new();
        public List<decimal> Totals { get; } = new();
        public DateOnly? Closing { get; set; }
        public DateOnly? Due { get; set; }
        public DateOnly? NextClosing { get; set; }
        public InvoiceCardHeader? Card { get; set; }
        public DateOnly? LastDate { get; set; }
        /// <summary>Compras no exterior da seção ainda sem cotação: a linha COTAÇÃO vem depois delas.</summary>
        public List<int> AwaitingRate { get; } = new();
        public int? LastForeign { get; set; }
    }

    public static ParsedCardInvoiceResponse Read(string? text, DateOnly closingDate)
    {
        var state = new State();
        var errors = new List<ImportLineError>();

        var lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            if (raw.Contains(';') || raw.Contains('\t'))
            {
                var structured = ReadStructured(raw, closingDate, out var error);
                if (structured is null) errors.Add(new ImportLineError(lineNumber, raw, error!));
                else
                {
                    state.Items.Add(structured);
                    state.LastDate = structured.Date;
                }
                continue;
            }

            // Datas da fatura: fechamento, vencimento e o próximo fechamento, quando ela os traz.
            if (TryInvoiceDates(raw, closingDate, state)) continue;

            if (TryCardHeader(raw) is { } header)
            {
                CloseSection(state);
                state.Card = header;
                if (!state.Cards.Any(c => c.LastFour == header.LastFour)) state.Cards.Add(header);
                continue;
            }

            if (TryRate(raw) is { } rate)
            {
                foreach (var index in state.AwaitingRate)
                    state.Items[index] = state.Items[index] with { ExchangeRate = rate, ExchangeRateEstimated = false };
                state.AwaitingRate.Clear();
                continue;
            }

            if (TotalLine().IsMatch(raw))
            {
                var money = Spaces().Replace(RealSymbol().Replace(raw, "$1"), " ").Split(' ').FirstOrDefault(ImportValueParser.IsMoneyToken);
                if (money is not null && ImportValueParser.TryParseMoney(money, out var total)) state.Totals.Add(total);
                continue;
            }

            foreach (var segment in TwoColumns().Split(raw))
            {
                var item = ReadFreeForm(segment.Trim(), closingDate, state, out var error);
                if (error is not null) errors.Add(new ImportLineError(lineNumber, raw, error));
                if (item is null) continue;
                state.Items.Add(item);
                state.LastDate = item.Date;
                if (item.ForeignAmount is not null)
                {
                    state.LastForeign = state.Items.Count - 1;
                    state.AwaitingRate.Add(state.Items.Count - 1);
                }
            }
        }
        CloseSection(state);

        // Um VALOR TOTAL só (ou todos iguais) é o total da fatura; vários diferentes seriam
        // subtotais, e somar seria chute.
        decimal? totalFromText = state.Totals.Distinct().Count() == 1 ? state.Totals[0] : null;

        return new ParsedCardInvoiceResponse(state.Items, errors, state.Items.Sum(x => x.Amount), state.Cards, totalFromText,
            ClosingDateFromText: state.Closing, DueDateFromText: state.Due, NextClosingDateFromText: state.NextClosing);
    }

    /// <summary>Fim de uma seção de cartão: compras no exterior que ficaram sem COTAÇÃO ganham a calculada.</summary>
    private static void CloseSection(State state)
    {
        foreach (var index in state.AwaitingRate)
        {
            var item = state.Items[index];
            if (item.ForeignAmount is { } foreign && foreign != 0)
                state.Items[index] = item with { ExchangeRate = Math.Round(Math.Abs(item.Amount / foreign), 4), ExchangeRateEstimated = true };
        }
        state.AwaitingRate.Clear();
        state.LastForeign = null;
    }

    private static CardInvoiceItemRequest? ReadFreeForm(string segment, DateOnly closingDate, State state, out string? error)
    {
        error = null;
        if (segment.Length == 0) return null;

        var line = Spaces().Replace(RealSymbol().Replace(segment, "$1"), " ");
        var tokens = line.Split(' ').ToList();

        // Dígito de modalidade antes da data (1, 2 ou 3): aceito e descartado.
        if (tokens.Count > 1 && (tokens[0] is "1" or "2" or "3") && ImportValueParser.LooksLikeDate(tokens[1]))
        {
            tokens.RemoveAt(0);
        }

        DateOnly? date = null;
        if (tokens.Count > 0 && ImportValueParser.LooksLikeDate(tokens[0]))
        {
            if (!ImportValueParser.TryParseDate(tokens[0], closingDate, out var parsed))
            {
                error = $"Data inválida: \"{tokens[0]}\".";
                return null;
            }
            date = parsed;
            tokens.RemoveAt(0);
        }

        // Até dois valores no fim: reais e, opcionalmente, a moeda estrangeira. Um valor logo depois
        // de um código de moeda ("EUR 19,50") é da descrição, não coluna.
        var trailing = new List<string>();
        for (var k = tokens.Count - 1; k >= 0 && trailing.Count < 2; k--)
        {
            if (!ImportValueParser.IsMoneyToken(tokens[k])) break;
            if (k > 0 && Currencies.Contains(tokens[k - 1])) break;
            trailing.Insert(0, tokens[k]);
        }
        if (trailing.Count == 0) return null;   // sem valor: cabeçalho, título de seção

        var isIof = tokens.Count > 0 && tokens[0].Equals("IOF", StringComparison.OrdinalIgnoreCase);
        if (date is null && !isIof) return null; // resumo, limite — não é item

        var effectiveDate = date ?? state.LastDate;
        if (effectiveDate is null)
        {
            error = "IOF sem uma compra anterior de onde herdar a data.";
            return null;
        }

        tokens.RemoveRange(tokens.Count - trailing.Count, trailing.Count);
        ImportValueParser.TryParseMoney(trailing[0], out var amount);
        decimal? foreign = null;
        if (trailing.Count == 2 && ImportValueParser.TryParseMoney(trailing[1], out var f) && f != 0) foreign = f;

        var installment = ExtractInstallment(tokens);

        var description = string.Join(' ', tokens).Trim();
        if (description.Length == 0)
        {
            error = "Item sem descrição.";
            return null;
        }

        var (originalAmount, originalCurrency) = OriginalCurrency(description, foreign is null ? null : DefaultForeignCurrency);
        var kind = Classify(description, amount, installment);
        var parsedLabel = InstallmentLabel.TryParse(installment);

        return new CardInvoiceItemRequest(effectiveDate.Value, description, installment, amount,
            CardLastFour: state.Card?.LastFour,
            Kind: kind,
            ForeignAmount: foreign,
            ForeignCurrency: foreign is null ? null : DefaultForeignCurrency,
            OriginalAmount: originalAmount,
            OriginalCurrency: originalCurrency,
            // O IOF de compra no exterior fica ligado à última compra no exterior da seção.
            RelatedIndex: kind == CardInvoiceItemKind.Tax ? state.LastForeign : null,
            InstallmentNumber: parsedLabel?.Number,
            InstallmentTotal: parsedLabel?.Total);
    }

    /// <summary>
    /// O tipo da linha, pela descrição e pelo sinal. Negativo é pagamento ou crédito; IOF é imposto;
    /// anuidade, tarifa, seguro e encargos são tarifa (mesmo parcelados, como a anuidade em 12x).
    /// </summary>
    public static CardInvoiceItemKind Classify(string description, decimal amount, string? installmentLabel)
    {
        var d = description.ToUpperInvariant();
        if (amount < 0)
            return d.Contains("PAGAMENTO") || d.Contains("PAGTO") ? CardInvoiceItemKind.Payment : CardInvoiceItemKind.Credit;
        if (d.StartsWith("IOF")) return CardInvoiceItemKind.Tax;
        if (FeeWords().IsMatch(d)) return CardInvoiceItemKind.Fee;
        return InstallmentLabel.TryParse(installmentLabel) is not null ? CardInvoiceItemKind.Installment : CardInvoiceItemKind.Expense;
    }

    /// <summary>"EUR 19,50" ou "19,50 EUR" na descrição — só se for outra moeda que a da coluna.</summary>
    private static (decimal? amount, string? currency) OriginalCurrency(string description, string? columnCurrency)
    {
        foreach (Match m in CurrencyInText().Matches(description))
        {
            var code = (m.Groups["c1"].Success ? m.Groups["c1"].Value : m.Groups["c2"].Value).ToUpperInvariant();
            var value = m.Groups["v1"].Success ? m.Groups["v1"].Value : m.Groups["v2"].Value;
            if (!Currencies.Contains(code) || code.Equals(columnCurrency, StringComparison.OrdinalIgnoreCase)) continue;
            if (code == "BRL") continue;
            if (ImportValueParser.TryParseMoney(value, out var amount)) return (amount, code);
        }
        return (null, null);
    }

    /// <summary>
    /// Cabeçalho de cartão: "PORTADOR FICTICIO - 5480 XXXX XXXX 1111", "@ PORTADOR - 5480 XXXX XXXX
    /// 7310" (virtual), "Cartão final 1234", "PORTADOR **** 1234". Linha sem valor nem data.
    /// </summary>
    public static InvoiceCardHeader? TryCardHeader(string line)
    {
        if (line.Split(' ').Any(ImportValueParser.IsMoneyToken)) return null;
        if (line.Length > 0 && ImportValueParser.LooksLikeDate(line.Split(' ')[0])) return null;

        var isVirtual = line.Contains('@');
        var clean = line.Replace("@", " ").Trim();

        var full = MaskedNumber().Match(clean);
        if (full.Success)
            return new InvoiceCardHeader(full.Groups["last"].Value, full.Groups["first"].Value, HolderName(clean[..full.Index]), isVirtual);

        var finalWord = FinalWord().Match(clean);
        if (finalWord.Success)
            return new InvoiceCardHeader(finalWord.Groups["last"].Value, null, HolderName(clean[..finalWord.Index]), isVirtual);

        var masked = MaskedTail().Match(clean);
        if (masked.Success)
            return new InvoiceCardHeader(masked.Groups["last"].Value, null, HolderName(clean[..masked.Index]), isVirtual);

        return null;
    }

    private static string? HolderName(string before)
    {
        var name = Regex.Replace(before, @"(?i)\b(cart[aã]o|n[uú]mero|final)\b", " ");
        name = Spaces().Replace(name.Trim(' ', '-', '–', ':'), " ").Trim(' ', '-', '–', ':');
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// "Data de fechamento: 02/09/2026", "Vencimento 10/09", "Próximo fechamento 02/10" — linha sem
    /// valor. Devolve true quando a linha era uma dessas (e não deve ser lida como item).
    /// </summary>
    private static bool TryInvoiceDates(string line, DateOnly reference, State state)
    {
        if (line.Split(' ').Any(ImportValueParser.IsMoneyToken)) return false;

        var next = NextClosingLine().Match(line);
        if (next.Success && ParseLooseDate(next.Groups["d"].Value, reference, forward: true) is { } n)
        {
            state.NextClosing ??= n;
            return true;
        }
        var closing = ClosingLine().Match(line);
        if (closing.Success && !line.Contains("próxim", StringComparison.OrdinalIgnoreCase) && !line.Contains("proxim", StringComparison.OrdinalIgnoreCase)
            && ParseLooseDate(closing.Groups["d"].Value, reference, forward: false) is { } c)
        {
            state.Closing ??= c;
            return true;
        }
        var due = DueLine().Match(line);
        if (due.Success && !line.Contains("próxim", StringComparison.OrdinalIgnoreCase) && !line.Contains("proxim", StringComparison.OrdinalIgnoreCase)
            && ParseLooseDate(due.Groups["d"].Value, reference, forward: true) is { } v)
        {
            state.Due ??= v;
            return true;
        }
        return false;
    }

    /// <summary>
    /// dd/MM sem ano: o vencimento e o próximo fechamento são DEPOIS do fechamento informado (podem
    /// virar o ano); o fechamento, perto dele.
    /// </summary>
    private static DateOnly? ParseLooseDate(string raw, DateOnly reference, bool forward)
    {
        if (raw.Length > 5) return ImportValueParser.TryParseDate(raw, reference, out var full) ? full : null;
        var parts = raw.Split('/');
        if (!int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month) || month is < 1 or > 12) return null;
        foreach (var year in forward ? new[] { reference.Year, reference.Year + 1 } : new[] { reference.Year, reference.Year - 1, reference.Year + 1 })
        {
            if (day > DateTime.DaysInMonth(year, month)) continue;
            var candidate = new DateOnly(year, month, day);
            if (forward ? candidate >= reference.AddDays(-3) : Math.Abs(candidate.DayNumber - reference.DayNumber) <= 45) return candidate;
        }
        return null;
    }

    private static decimal? TryRate(string line)
    {
        var m = RateLine().Match(line);
        if (!m.Success) return null;
        return decimal.TryParse(m.Groups["rate"].Value.Replace(".", "").Replace(',', '.'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var rate) && rate > 0 ? rate : null;
    }

    /// <summary>
    /// Parcela em qualquer dos três formatos vistos nas faturas, devolvida sempre como
    /// <c>NN/NN</c>. Tira o trecho da descrição.
    /// </summary>
    private static string? ExtractInstallment(List<string> tokens)
    {
        if (tokens.Count == 0) return null;

        // "04/06" como coluna, logo antes do valor.
        if (InstallmentToken().IsMatch(tokens[^1]))
        {
            var label = tokens[^1];
            tokens.RemoveAt(tokens.Count - 1);
            // "Parcela 02/02": a palavra antes também sai.
            if (tokens.Count > 0 && tokens[^1].Equals("Parcela", StringComparison.OrdinalIgnoreCase))
                tokens.RemoveAt(tokens.Count - 1);
            return label;
        }

        // "titular05-12": grudada no fim da última palavra.
        var glued = GluedInstallment().Match(tokens[^1]);
        if (glued.Success)
        {
            var prefix = tokens[^1][..glued.Index];
            tokens[^1] = prefix;
            if (prefix.Length == 0) tokens.RemoveAt(tokens.Count - 1);
            return $"{glued.Groups[1].Value}/{glued.Groups[2].Value}";
        }

        return null;
    }

    private static CardInvoiceItemRequest? ReadStructured(string raw, DateOnly closingDate, out string? error)
    {
        error = null;
        var fields = raw.Split(raw.Contains('\t') ? '\t' : ';').Select(f => f.Trim()).ToArray();

        // 3 campos: data;descrição;valor — 4: data;descrição;parcela;valor (formato antigo).
        if (fields.Length is < 3 or > 4)
        {
            error = "Formato não reconhecido: use data;descrição;valor (ou data;descrição;parcela;valor).";
            return null;
        }
        if (!ImportValueParser.TryParseDate(fields[0], closingDate, out var date))
        {
            error = $"Data inválida: \"{fields[0]}\".";
            return null;
        }
        if (!ImportValueParser.TryParseMoney(fields[^1], out var amount))
        {
            error = $"Valor inválido: \"{fields[^1]}\".";
            return null;
        }
        if (fields[1].Length == 0)
        {
            error = "Item sem descrição.";
            return null;
        }

        var installment = fields.Length == 4 && fields[2].Length > 0 ? fields[2] : null;
        var parsedLabel = InstallmentLabel.TryParse(installment);
        return new CardInvoiceItemRequest(date, fields[1], installment, amount,
            Kind: Classify(fields[1], amount, installment),
            InstallmentNumber: parsedLabel?.Number, InstallmentTotal: parsedLabel?.Total);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"pr[oó]xim[oa]\s+(?:fatura\s+)?(?:fechamento|fecha|corte)\D{0,25}?(?<d>\d{2}/\d{2}(?:/\d{2,4})?)", RegexOptions.IgnoreCase)]
    private static partial Regex NextClosingLine();

    [GeneratedRegex(@"(?:fechamento|data\s+de\s+corte)\D{0,25}?(?<d>\d{2}/\d{2}(?:/\d{2,4})?)", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingLine();

    [GeneratedRegex(@"vencimento\D{0,25}?(?<d>\d{2}/\d{2}(?:/\d{2,4})?)", RegexOptions.IgnoreCase)]
    private static partial Regex DueLine();

    [GeneratedRegex(@"(-?R\$)\s+")]
    private static partial Regex RealSymbol();

    [GeneratedRegex(@"^\d{2}/\d{2}$")]
    private static partial Regex InstallmentToken();

    [GeneratedRegex(@"(\d{2})-(\d{2})$")]
    private static partial Regex GluedInstallment();

    // Um valor seguido de 2+ espaços e de uma nova data: a linha tem duas colunas.
    [GeneratedRegex(@"(?<=\d,\d{2}-?)\s{2,}(?=(?:[1-3]\s+)?\d{2}/\d{2}\s)")]
    private static partial Regex TwoColumns();

    [GeneratedRegex(@"(?<first>\d{4})\s*[X*•.]{4}\s*[X*•.]{4}\s*(?<last>\d{4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MaskedNumber();

    [GeneratedRegex(@"\bfinal\s*:?\s*(?<last>\d{4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FinalWord();

    [GeneratedRegex(@"[X*•]{4}\s*(?<last>\d{4})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MaskedTail();

    [GeneratedRegex(@"^COTA[ÇC][ÃA]O\s+(?:DO\s+)?D[OÓ]LAR\D*(?<rate>\d+,\d{2,6})", RegexOptions.IgnoreCase)]
    private static partial Regex RateLine();

    [GeneratedRegex(@"^(VALOR\s+TOTAL|TOTAL\s+DA\s+FATURA|TOTAL\s+DESTA\s+FATURA)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TotalLine();

    [GeneratedRegex(@"\b(ANUIDADE|TARIFA|SEGURO|JUROS|MULTA|ENCARGO|ENCARGOS)\b")]
    private static partial Regex FeeWords();

    [GeneratedRegex(@"\b(?:(?<c1>[A-Z]{3})\s*(?<v1>\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})|(?<v2>\d{1,3}(?:\.\d{3})*,\d{2}|\d+,\d{2})\s*(?<c2>[A-Z]{3}))\b", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyInText();
}
