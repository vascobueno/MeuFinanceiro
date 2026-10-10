using System.Text.RegularExpressions;

namespace MeuFinanceiro.Application.Reconciliation.Import;

/// <summary>
/// Lê movimentos colados de um extrato bancário (docs/09-conciliacao-conta-corrente.md).
///
/// Aceita dois jeitos de colar:
/// <list type="bullet">
/// <item>com separador (<c>;</c> ou tabulação) — o formato antigo
/// <c>data;descrição;tipo;valor</c> continua valendo;</item>
/// <item>o texto como sai do extrato: <c>dd/MM DESCRIÇÃO [nº documento] movimento [saldo]</c>,
/// com o débito marcado por <c>-</c> no fim do valor.</item>
/// </list>
///
/// Regras tiradas de extratos reais: a data só aparece na primeira linha de cada dia (as
/// seguintes herdam); a descrição pode continuar na linha de baixo (uma linha sem valor é
/// continuação — é assim que a compra no débito traz o estabelecimento); documento <c>-</c>
/// significa "sem documento"; as linhas <c>SALDO EM</c> não são movimentos, mas dão os saldos.
/// </summary>
public static partial class StatementTextReader
{
    private const int MinDocumentDigits = 5;

    /// <summary>Quantos dias a compra no débito pode ter antes do lançamento no extrato.</summary>
    private const int MaxPurchaseLagDays = 10;

    public static ParsedBankStatementResponse Read(string? text, DateOnly referenceStart, DateOnly referenceEnd)
    {
        var items = new List<MutableItem>();
        var errors = new List<ImportLineError>();
        decimal? opening = null;
        decimal? closing = null;
        DateOnly? lastDate = null;

        var lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            if (raw.Contains(';') || raw.Contains('\t'))
            {
                var structured = ReadStructured(raw, referenceEnd, out var error);
                if (structured is null) errors.Add(new ImportLineError(lineNumber, raw, error!));
                else
                {
                    items.Add(structured);
                    lastDate = structured.Date;
                }
                continue;
            }

            var line = Spaces().Replace(RealSymbol().Replace(raw, "$1"), " ");
            if (IsNoise(line)) continue;

            var tokens = line.Split(' ').ToList();

            // Data no início (opcional).
            DateOnly? date = null;
            if (tokens.Count > 0 && ImportValueParser.LooksLikeDate(tokens[0]))
            {
                if (!ImportValueParser.TryParseDate(tokens[0], referenceEnd, out var parsed))
                {
                    errors.Add(new ImportLineError(lineNumber, raw, $"Data inválida: \"{tokens[0]}\"."));
                    continue;
                }
                date = parsed;
                tokens.RemoveAt(0);
            }

            // Saldos: a primeira linha de saldo antes de qualquer movimento é o inicial; a
            // última depois deles, o final.
            if (BalanceLine().IsMatch(string.Join(' ', tokens)))
            {
                var money = TrailingMoney(tokens);
                if (money.Count > 0 && ImportValueParser.TryParseMoney(money[0], out var balance))
                {
                    if (items.Count == 0) opening = balance;
                    else closing = balance;
                }
                continue;
            }

            var trailing = TrailingMoney(tokens);
            if (trailing.Count == 0)
            {
                // Sem valor: continuação da descrição do movimento anterior.
                if (items.Count > 0 && HasLetters(line))
                {
                    var previous = items[^1];
                    // Na compra no débito a continuação é "dd/MM ESTABELECIMENTO": a data da
                    // compra, que pode ser dias antes do lançamento (compra no sábado, débito na
                    // segunda). Vira EffectiveDate e sai da descrição.
                    if (date is not null && !previous.PurchaseDateRead
                        && StatementItemClassifier.Classify(previous.Description, previous.Amount) == StatementItemClassifier.DebitCardPurchase
                        && date.Value <= previous.Date && previous.Date.DayNumber - date.Value.DayNumber <= MaxPurchaseLagDays)
                    {
                        previous.PurchaseDateRead = true;
                        if (date.Value < previous.Date) previous.EffectiveDate = date;
                        previous.Description = $"{previous.Description} {string.Join(' ', tokens)}".Trim();
                    }
                    else
                    {
                        previous.Description = $"{previous.Description} {line}".Trim();
                    }
                }
                else if (date is not null)
                {
                    errors.Add(new ImportLineError(lineNumber, raw, "Linha com data mas sem valor, e nenhum movimento anterior para completar."));
                }
                continue;
            }

            // Movimento e, se houver, saldo do dia depois dele.
            tokens.RemoveRange(tokens.Count - trailing.Count, trailing.Count);
            ImportValueParser.TryParseMoney(trailing[0], out var amount);

            string? document = null;
            if (tokens.Count > 0 && IsDocument(tokens[^1]))
            {
                document = tokens[^1] == "-" ? null : tokens[^1];
                tokens.RemoveAt(tokens.Count - 1);
            }

            var effectiveDate = date ?? lastDate;
            if (effectiveDate is null)
            {
                errors.Add(new ImportLineError(lineNumber, raw, "Movimento sem data, e nenhuma linha anterior com data para herdar."));
                continue;
            }

            var description = string.Join(' ', tokens).Trim();
            if (description.Length == 0)
            {
                errors.Add(new ImportLineError(lineNumber, raw, "Movimento sem descrição."));
                continue;
            }

            items.Add(new MutableItem(effectiveDate.Value, description, document, null, amount));
            lastDate = effectiveDate;
        }

        var result = items.Select(i => new BankStatementItemRequest(
            i.Date, i.Description, i.Document,
            StatementItemClassifier.Resolve(i.ItemType, i.Description, i.Amount), i.Amount,
            i.EffectiveDate)).ToList();

        return new ParsedBankStatementResponse(result, errors, opening, closing, result.Sum(i => i.Amount));
    }

    /// <summary>Até dois valores no fim da linha: o movimento e, opcionalmente, o saldo do dia.</summary>
    private static List<string> TrailingMoney(List<string> tokens)
    {
        var found = new List<string>();
        for (var k = tokens.Count - 1; k >= 0 && found.Count < 2; k--)
        {
            if (!ImportValueParser.IsMoneyToken(tokens[k])) break;
            found.Insert(0, tokens[k]);
        }
        return found;
    }

    private static MutableItem? ReadStructured(string raw, DateOnly reference, out string? error)
    {
        error = null;
        var fields = raw.Split(raw.Contains('\t') ? '\t' : ';').Select(f => f.Trim()).ToArray();

        // 3 campos: data;descrição;valor — 4: data;descrição;tipo;valor (formato antigo) —
        // 5: data;descrição;documento;tipo;valor.
        if (fields.Length is < 3 or > 5)
        {
            error = "Formato não reconhecido: use data;descrição;valor (ou data;descrição;tipo;valor).";
            return null;
        }

        if (!ImportValueParser.TryParseDate(fields[0], reference, out var date))
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
            error = "Movimento sem descrição.";
            return null;
        }

        string? document = fields.Length == 5 && fields[2] is not ("" or "-") ? fields[2] : null;
        string? type = fields.Length switch
        {
            4 => NullIfEmpty(fields[2]),
            5 => NullIfEmpty(fields[3]),
            _ => null
        };

        return new MutableItem(date, fields[1], document, type, amount);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.ToUpperInvariant();

    private static bool IsDocument(string token) =>
        token == "-" || (token.Length >= MinDocumentDigits && token.All(char.IsDigit));

    private static bool HasLetters(string line) => line.Any(char.IsLetter);

    private static bool IsNoise(string line) => NoiseLine().IsMatch(line);

    private sealed class MutableItem(DateOnly date, string description, string? document, string? itemType, decimal amount)
    {
        public DateOnly Date { get; } = date;
        public string Description { get; set; } = description;
        public string? Document { get; } = document;
        public string? ItemType { get; } = itemType;
        public decimal Amount { get; } = amount;
        public DateOnly? EffectiveDate { get; set; }
        public bool PurchaseDateRead { get; set; }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    // "R$ 54,00" e "-R$ 54,00" viram um token só.
    [GeneratedRegex(@"(-?R\$)\s+")]
    private static partial Regex RealSymbol();

    [GeneratedRegex(@"^SALDO\s+(EM\s+\d{2}/\d{2}|ANTERIOR)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BalanceLine();

    // Cabeçalhos e rodapés de página que, colados junto, virariam continuação de descrição.
    [GeneratedRegex(@"(^data\s+descri)|(^p[aá]gina\b)|(^extrato\b)|(^movimenta)|(\(r\$\))|(^[a-zç]+/\d{4}$)|(^conta corrente$)", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseLine();
}
