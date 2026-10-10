using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Lê itens colados de uma fatura de cartão (docs/08-conciliacao-cartoes.md).
///
/// Formatos tirados de faturas reais de dois emissores:
/// <list type="bullet">
/// <item><c>[1-3] dd/MM DESCRIÇÃO [NN/NN] valor [US$]</c> — o dígito antes da data é aceito e
/// ignorado; o segundo valor, quando existe, é o em dólar (não é lido como reais);</item>
/// <item><c>dd/MM Descrição [Parcela NN/NN] [-]R$ valor</c>;</item>
/// <item>parcela grudada no fim da descrição: <c>Anuidade Diferenc.titular05-12</c>;</item>
/// <item><c>IOF DESPESA NO EXTERIOR valor</c>, sem data — herda a da compra anterior.</item>
/// </list>
///
/// Linhas sem data e sem ser de IOF (totais, resumo, cotação, cabeçalhos) são ignoradas: numa
/// fatura, ao contrário do extrato, não há descrição que continue na linha de baixo. Duas
/// colunas na mesma linha (como sai ao copiar alguns PDFs) são separadas em dois itens.
/// </summary>
public static partial class InvoiceTextReader
{
    public static ParsedCardInvoiceResponse Read(string? text, DateOnly closingDate)
    {
        var items = new List<CardInvoiceItemRequest>();
        var errors = new List<ImportLineError>();
        DateOnly? lastDate = null;

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
                    items.Add(structured);
                    lastDate = structured.Date;
                }
                continue;
            }

            foreach (var segment in TwoColumns().Split(raw))
            {
                var item = ReadFreeForm(segment.Trim(), closingDate, lastDate, out var error);
                if (error is not null) errors.Add(new ImportLineError(lineNumber, raw, error));
                if (item is null) continue;
                items.Add(item);
                lastDate = item.Date;
            }
        }

        return new ParsedCardInvoiceResponse(items, errors, items.Sum(x => x.Amount));
    }

    private static CardInvoiceItemRequest? ReadFreeForm(string segment, DateOnly closingDate, DateOnly? lastDate, out string? error)
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

        // Até dois valores no fim: reais e, opcionalmente, dólar.
        var trailing = new List<string>();
        for (var k = tokens.Count - 1; k >= 0 && trailing.Count < 2; k--)
        {
            if (!ImportValueParser.IsMoneyToken(tokens[k])) break;
            trailing.Insert(0, tokens[k]);
        }
        if (trailing.Count == 0) return null;   // sem valor: cabeçalho, título de seção, cotação

        var isIof = tokens.Count > 0 && tokens[0].Equals("IOF", StringComparison.OrdinalIgnoreCase);
        if (date is null && !isIof) return null; // total, resumo, limite — não é item

        var effectiveDate = date ?? lastDate;
        if (effectiveDate is null)
        {
            error = "IOF sem uma compra anterior de onde herdar a data.";
            return null;
        }

        tokens.RemoveRange(tokens.Count - trailing.Count, trailing.Count);
        ImportValueParser.TryParseMoney(trailing[0], out var amount);

        var installment = ExtractInstallment(tokens);

        var description = string.Join(' ', tokens).Trim();
        if (description.Length == 0)
        {
            error = "Item sem descrição.";
            return null;
        }

        return new CardInvoiceItemRequest(effectiveDate.Value, description, installment, amount);
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
        return new CardInvoiceItemRequest(date, fields[1], installment, amount);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"(-?R\$)\s+")]
    private static partial Regex RealSymbol();

    [GeneratedRegex(@"^\d{2}/\d{2}$")]
    private static partial Regex InstallmentToken();

    [GeneratedRegex(@"(\d{2})-(\d{2})$")]
    private static partial Regex GluedInstallment();

    // Um valor seguido de 2+ espaços e de uma nova data: a linha tem duas colunas.
    [GeneratedRegex(@"(?<=\d,\d{2}-?)\s{2,}(?=(?:[1-3]\s+)?\d{2}/\d{2}\s)")]
    private static partial Regex TwoColumns();
}
